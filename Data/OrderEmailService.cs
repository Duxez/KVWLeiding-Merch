using System.Text;
using System.Net;
using System.Globalization;
using MailKit.Net.Smtp;
using MailKit.Security;
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

    public OrderEmailService(IOptions<EmailSettings> emailOptions)
    {
        _settings = emailOptions.Value;
    }

    public async Task SendOrderAsync(string customerEmail, IReadOnlyList<CartItem> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Cannot send an empty order.");

        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
            throw new InvalidOperationException("Email:SmtpHost is not configured.");

        if (string.IsNullOrWhiteSpace(_settings.OrdersRecipientAddress))
            throw new InvalidOperationException("Email:OrdersRecipientAddress is not configured.");

        var fromAddress = ResolveFromAddress();

        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            customerEmail.Trim(),
            _settings.OrdersRecipientAddress.Trim(),
        };

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName ?? "KVW Merch", fromAddress));

        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = "KVW Merch order";

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
            body.AppendLine($"{i + 1}. {item.ProductTitle} | Size: {item.SizeName} | Color: {item.ColorName} | Price: {FormatMoney(item.UnitPrice)}");
        }

        body.AppendLine();
        body.AppendLine($"Total: {FormatMoney(total)}");

        return body.ToString();
    }

    private static string BuildHtmlBody(string customerEmail, IReadOnlyList<CartItem> items)
    {
        var itemsHtml = new StringBuilder();
        var total = items.Sum(i => i.UnitPrice);

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var productTitle = HtmlEncode(item.ProductTitle);
            var sizeName = HtmlEncode(item.SizeName);
            var colorName = HtmlEncode(item.ColorName);
            var colorHex = NormalizeHex(item.ColorHex);
            var linePrice = FormatMoney(item.UnitPrice);

            itemsHtml.Append($"""
                <div style="display:flex;align-items:center;justify-content:space-between;gap:8px;padding:10px 0;border-bottom:1px solid #e0e0e0;">
                    <div style="display:flex;flex-direction:column;gap:4px;">
                        <div style="font-size:16px;font-weight:600;color:#1f2328;">{productTitle}</div>
                        <div style="display:flex;flex-wrap:wrap;gap:6px;">
                            <span style="display:inline-block;padding:4px 10px;border-radius:999px;background:#0f6cbd;color:#ffffff;font-size:12px;font-weight:600;">{sizeName}</span>
                            <span style="display:inline-flex;align-items:center;padding:4px 10px;border-radius:999px;background:#f3f2f1;color:#323130;font-size:12px;font-weight:600;border:1px solid #e1dfdd;">
                                <span style="display:inline-block;width:10px;height:10px;border-radius:50%;margin-right:6px;border:1px solid rgba(0,0,0,0.2);background:{colorHex};"></span>
                                {colorName}
                            </span>
                        </div>
                    </div>
                    <div style="font-size:14px;font-weight:700;color:#1f2328;white-space:nowrap;">{linePrice}</div>
                </div>
                """);
        }

        return $"""
            <!doctype html>
            <html>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:'Segoe UI',Arial,sans-serif;color:#1f2328;">
                <div style="max-width:640px;margin:24px auto;padding:0 12px;">
                    <div style="background:#ffffff;border:1px solid #e1dfdd;border-radius:12px;overflow:hidden;">
                        <div style="padding:16px 20px;background:#0f6cbd;color:#ffffff;font-size:18px;font-weight:700;">KVW Merch order</div>
                        <div style="padding:16px 20px;">
                            <div style="font-size:14px;color:#605e5c;margin-bottom:4px;">Customer email</div>
                            <div style="font-size:15px;font-weight:600;margin-bottom:14px;">{HtmlEncode(customerEmail)}</div>
                            <div style="font-size:14px;color:#605e5c;margin-bottom:4px;">Order date (UTC)</div>
                            <div style="font-size:15px;font-weight:600;margin-bottom:14px;">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</div>
                            <div style="font-size:15px;font-weight:700;margin-bottom:4px;">Items ({items.Count})</div>
                            <div>{itemsHtml}</div>
                            <div style="display:flex;justify-content:flex-end;padding-top:12px;">
                                <span style="font-size:16px;font-weight:700;color:#1f2328;">Total: {FormatMoney(total)}</span>
                            </div>
                        </div>
                    </div>
                </div>
            </body>
            </html>
            """;
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
