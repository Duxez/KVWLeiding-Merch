using System.Text;
using System.Net;
using System.Globalization;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace kvwleidingmerch.Data;

public interface IOrderEmailService
{
    Task SendOrderAsync(string customerEmail, IReadOnlyList<CartItem> items, CancellationToken cancellationToken = default);
}

public sealed class OrderEmailService : IOrderEmailService
{
    private readonly EmailSettings _settings;
    private readonly AppDbContext _dbContext;

    public OrderEmailService(IOptions<EmailSettings> emailOptions, AppDbContext dbContext)
    {
        _settings = emailOptions.Value;
        _dbContext = dbContext;
    }

    public async Task SendOrderAsync(string customerEmail, IReadOnlyList<CartItem> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Cannot send an empty order.");

        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
            throw new InvalidOperationException("Email:SmtpHost is not configured.");

        var adminEmails = await _dbContext.OrderRecipientEmails
            .Select(e => e.EmailAddress)
            .ToListAsync(cancellationToken);

        if (adminEmails.Count == 0)
            throw new InvalidOperationException("No order recipient emails configured in the database.");

        var fromAddress = ResolveFromAddress();

        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            customerEmail.Trim(),
        };

        foreach (var adminEmail in adminEmails)
        {
            if (!string.IsNullOrWhiteSpace(adminEmail))
            {
                recipients.Add(adminEmail.Trim());
            }
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName ?? "KVW Merch", fromAddress));

        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = (_settings.Title + " order") ?? "KVW Merch order";

        var bodyBuilder = new BodyBuilder
        {
            TextBody = BuildPlainTextBody(customerEmail, items),
            HtmlBody = BuildHtmlBody(customerEmail, items),
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var smtpClient = new SmtpClient();
        var secureSocketOptions = _settings.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;
        smtpClient.LocalEndPoint = new IPEndPoint(IPAddress.Any, 0);
        await smtpClient.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secureSocketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_settings.UserName))
        {
            await smtpClient.AuthenticateAsync(
                _settings.UserName,
                _settings.Password ?? string.Empty,
                cancellationToken);
        }

        await smtpClient.SendAsync(message, cancellationToken);
        await smtpClient.DisconnectAsync(true, cancellationToken);
    }

    private string ResolveFromAddress()
    {
        if (!string.IsNullOrWhiteSpace(_settings.FromAddress))
            return _settings.FromAddress.Trim();

        if (!string.IsNullOrWhiteSpace(_settings.UserName) && _settings.UserName.Contains('@'))
            return _settings.UserName.Trim();

        throw new InvalidOperationException("Email:FromAddress is not configured.");
    }

    private static string BuildPlainTextBody(string customerEmail, IReadOnlyList<CartItem> items)
    {
        var body = new StringBuilder();
        var total = items.Sum(i => i.UnitPrice);

        body.AppendLine("A new order was placed.");
        body.AppendLine();
        body.AppendLine($"Customer email: {customerEmail}");
        body.AppendLine($"Order date (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        body.AppendLine();
        body.AppendLine("Items:");

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var sizeInfo = string.IsNullOrWhiteSpace(item.SizeName) ? "" : $" | Size: {item.SizeName}";
            var colorInfo = string.IsNullOrWhiteSpace(item.ColorName) ? "" : $" | Color: {item.ColorName}";
            body.AppendLine($"{i + 1}. {item.ProductTitle}{sizeInfo}{colorInfo} | Price: {FormatMoney(item.UnitPrice)}");
        }

        body.AppendLine();
        body.AppendLine($"Total: {FormatMoney(total)}");

        return body.ToString();
    }

    private static string BuildHtmlBody(string customerEmail, IReadOnlyList<CartItem> items)
    {
        var html = new StringBuilder();
        var total = items.Sum(i => i.UnitPrice);

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html>");
        html.AppendLine("<body style=\"margin:0;padding:0;background:#f5f5f5;font-family:'Segoe UI',Arial,sans-serif;color:#1f2328;\">");
        html.AppendLine("    <div style=\"max-width:640px;margin:24px auto;padding:0 12px;\">");
        html.AppendLine("        <div style=\"background:#ffffff;border:1px solid #e1dfdd;border-radius:12px;overflow:hidden;\">");
        html.AppendLine("            <div style=\"padding:16px 20px;background:#0f6cbd;color:#ffffff;font-size:18px;font-weight:700;\">KVW Merch order</div>");
        html.AppendLine("            <div style=\"padding:16px 20px;\">");
        html.AppendLine("                <div style=\"font-size:14px;color:#605e5c;margin-bottom:4px;\">Customer email</div>");
        html.Append($"                <div style=\"font-size:15px;font-weight:600;margin-bottom:14px;\">");
        html.Append(HtmlEncode(customerEmail));
        html.AppendLine("</div>");
        html.AppendLine("                <div style=\"font-size:14px;color:#605e5c;margin-bottom:4px;\">Order date (UTC)</div>");
        html.Append($"                <div style=\"font-size:15px;font-weight:600;margin-bottom:14px;\">");
        html.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        html.AppendLine("</div>");
        html.AppendLine($"                <div style=\"font-size:15px;font-weight:700;margin-bottom:4px;\">Items ({items.Count})</div>");
        html.AppendLine("                <div>");

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var productTitle = HtmlEncode(item.ProductTitle);
            var sizeName = HtmlEncode(item.SizeName);
            var colorName = HtmlEncode(item.ColorName);
            var colorHex = NormalizeHex(item.ColorHex);
            var linePrice = FormatMoney(item.UnitPrice);
            var hasSize = !string.IsNullOrWhiteSpace(sizeName);
            var hasColor = !string.IsNullOrWhiteSpace(colorName);

            html.AppendLine("                    <div style=\"display:flex;align-items:center;justify-content:space-between;gap:8px;padding:10px 0;border-bottom:1px solid #e0e0e0;\">");
            html.AppendLine("                        <div style=\"display:flex;flex-direction:column;gap:4px;\">");
            html.AppendLine($"                            <div style=\"font-size:16px;font-weight:600;color:#1f2328;\">{productTitle}</div>");
            
            if (hasSize || hasColor)
            {
                html.AppendLine("                            <div style=\"display:flex;flex-wrap:wrap;gap:6px;\">");
                if (hasSize)
                {
                    html.AppendLine($"                                <span style=\"display:inline-block;padding:4px 10px;border-radius:999px;background:#0f6cbd;color:#ffffff;font-size:12px;font-weight:600;\">{sizeName}</span>");
                }
                if (hasColor)
                {
                    html.Append("                                <span style=\"display:inline-flex;align-items:center;padding:4px 10px;border-radius:999px;background:#f3f2f1;color:#323130;font-size:12px;font-weight:600;border:1px solid #e1dfdd;\">");
                    if (!string.IsNullOrWhiteSpace(colorHex) && colorHex != "#d2d0ce")
                    {
                        html.Append($"<span style=\"display:inline-block;width:10px;height:10px;border-radius:50%;margin-right:6px;border:1px solid rgba(0,0,0,0.2);background:{colorHex};\"></span>");
                    }
                    html.Append(colorName);
                    html.AppendLine("</span>");
                }
                html.AppendLine("                            </div>");
            }
            
            html.AppendLine("                        </div>");
            html.AppendLine($"                        <div style=\"font-size:14px;font-weight:700;color:#1f2328;white-space:nowrap;\">{linePrice}</div>");
            html.AppendLine("                    </div>");
        }

        html.AppendLine("                </div>");
        html.AppendLine("                <div style=\"display:flex;justify-content:flex-end;padding-top:12px;\">");
        html.Append($"                    <span style=\"font-size:16px;font-weight:700;color:#1f2328;\">Total: {FormatMoney(total)}</span>");
        html.AppendLine("                </div>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static string HtmlEncode(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string NormalizeHex(string? value)
    {
        var hex = (value ?? string.Empty).Trim();
        if (hex.Length == 7 && hex[0] == '#')
            return hex;

        return "#d2d0ce";
    }

    private static string FormatMoney(decimal value)
        => value.ToString("C", CultureInfo.GetCultureInfo("nl-NL"));
}
