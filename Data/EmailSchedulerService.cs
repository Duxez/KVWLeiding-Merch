using System.Globalization;
using System.Net;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using WebUtility = System.Net.WebUtility;

namespace kvwleidingmerch.Data;

public interface IEmailSchedulerService
{
    Task ScheduleEmailAsync(DateTime scheduledTime, string timezone, CancellationToken cancellationToken = default);
    Task CheckAndSendPendingEmailsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledEmail>> GetScheduledEmailsAsync(CancellationToken cancellationToken = default);
    Task DeleteScheduledEmailAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class EmailSchedulerService : IEmailSchedulerService
{
    private readonly AppDbContext _dbContext;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailSchedulerService> _logger;

    public EmailSchedulerService(
        AppDbContext dbContext,
        IOptions<EmailSettings> emailOptions,
        ILogger<EmailSchedulerService> logger)
    {
        _dbContext = dbContext;
        _emailSettings = emailOptions.Value;
        _logger = logger;
    }

    public async Task ScheduleEmailAsync(DateTime scheduledTime, string timezone, CancellationToken cancellationToken = default)
    {
        var targetTimezone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var nowInTargetTz = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, targetTimezone);
        var scheduledInTargetTz = TimeZoneInfo.ConvertTimeFromUtc(scheduledTime, targetTimezone);
        
        if (scheduledInTargetTz < nowInTargetTz)
        {
            throw new InvalidOperationException("Scheduled time must be in the future.");
        }

        var scheduledEmail = new ScheduledEmail
        {
            Subject = "KVW Merch - Scheduled Order Summary",
            ScheduledTimeUtc = scheduledTime,
            Timezone = timezone,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.ScheduledEmails.Add(scheduledEmail);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Scheduled email {ScheduledEmailId} to be sent at {ScheduledTime} ({Timezone})", 
            scheduledEmail.Id, scheduledTime, timezone);
    }

    public async Task CheckAndSendPendingEmailsAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        
        var pendingEmails = await _dbContext.ScheduledEmails
            .Where(s => s.Status == "Pending" && s.ScheduledTimeUtc <= nowUtc)
            .OrderBy(s => s.ScheduledTimeUtc)
            .ToListAsync(cancellationToken);

        if (pendingEmails.Count == 0)
        {
            _logger.LogDebug("No pending scheduled emails to send.");
            return;
        }

        _logger.LogInformation("Found {Count} pending scheduled emails to process.", pendingEmails.Count);

        foreach (var scheduledEmail in pendingEmails)
        {
            try
            {
                await SendScheduledEmailAsync(scheduledEmail, cancellationToken);
                scheduledEmail.Status = "Completed";
                scheduledEmail.SentAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully sent scheduled email {ScheduledEmailId}", scheduledEmail.Id);
            }
            catch (Exception ex)
            {
                scheduledEmail.Status = "Failed";
                scheduledEmail.ErrorMessage = ex.Message;
                scheduledEmail.SentAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogError(ex, "Failed to send scheduled email {ScheduledEmailId}: {ErrorMessage}", 
                    scheduledEmail.Id, ex.Message);
            }
        }
    }

    public async Task<IReadOnlyList<ScheduledEmail>> GetScheduledEmailsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScheduledEmails
            .OrderByDescending(s => s.ScheduledTimeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteScheduledEmailAsync(int id, CancellationToken cancellationToken = default)
    {
        var scheduledEmail = await _dbContext.ScheduledEmails.FindAsync([id], cancellationToken);
        if (scheduledEmail != null)
        {
            _dbContext.ScheduledEmails.Remove(scheduledEmail);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deleted scheduled email {ScheduledEmailId}", id);
        }
    }

    private async Task SendScheduledEmailAsync(ScheduledEmail scheduledEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost))
            throw new InvalidOperationException("Email:SmtpHost is not configured.");

        var adminEmails = await _dbContext.OrderRecipientEmails
            .Select(e => e.EmailAddress)
            .ToListAsync(cancellationToken);

        if (adminEmails.Count == 0)
            throw new InvalidOperationException("No order recipient emails configured.");

        var fromAddress = ResolveFromAddress();

        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var adminEmail in adminEmails)
        {
            if (!string.IsNullOrWhiteSpace(adminEmail))
            {
                recipients.Add(adminEmail.Trim());
            }
        }

        if (recipients.Count == 0)
            throw new InvalidOperationException("No valid recipient email addresses.");

        var orders = await _dbContext.Orders
            .Include(o => o.Items)
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            _logger.LogWarning("No orders found to include in scheduled email.");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailSettings.FromName ?? "KVW Merch", fromAddress));

        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = scheduledEmail.Subject;

        var bodyBuilder = new BodyBuilder
        {
            TextBody = BuildPlainTextBody(orders),
            HtmlBody = BuildHtmlBody(orders),
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var smtpClient = new SmtpClient();
        smtpClient.LocalEndPoint = new IPEndPoint(IPAddress.Any, 0);
        await smtpClient.ConnectAsync(_emailSettings.SmtpHost, 587, SecureSocketOptions.StartTls, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_emailSettings.UserName))
        {
            await smtpClient.AuthenticateAsync(
                _emailSettings.UserName,
                _emailSettings.Password ?? string.Empty,
                cancellationToken);
        }

        await smtpClient.SendAsync(message, cancellationToken);
        await smtpClient.DisconnectAsync(true, cancellationToken);
    }

    private string ResolveFromAddress()
    {
        if (!string.IsNullOrWhiteSpace(_emailSettings.FromAddress))
            return _emailSettings.FromAddress.Trim();

        if (!string.IsNullOrWhiteSpace(_emailSettings.UserName) && _emailSettings.UserName.Contains('@'))
            return _emailSettings.UserName.Trim();

        throw new InvalidOperationException("Email:FromAddress is not configured.");
    }

    private static string BuildPlainTextBody(IReadOnlyList<Order> orders)
    {
        var body = new StringBuilder();
        var totalAll = orders.Sum(o => o.TotalAmount);

        body.AppendLine("Scheduled Order Summary");
        body.AppendLine();
        body.AppendLine($"Generated at (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        body.AppendLine();
        body.AppendLine($"Total orders: {orders.Count}");
        body.AppendLine();
        body.AppendLine("Orders:");

        for (int i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            body.AppendLine();
            body.AppendLine($"Order #{i + 1} - {order.CustomerEmail}");
            body.AppendLine($"  Date: {order.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}");
            body.AppendLine($"  Status: {order.Status}");
            body.AppendLine($"  Total: {FormatMoney(order.TotalAmount)}");
            body.AppendLine("  Items:");

            foreach (var item in order.Items)
            {
                body.AppendLine($"    - {item.ProductTitle} | Size: {item.SizeName} | Color: {item.ColorName} | Price: {FormatMoney(item.UnitPrice)}");
            }
        }

        body.AppendLine();
        body.AppendLine($"Total across all orders: {FormatMoney(totalAll)}");

        return body.ToString();
    }

    private static string BuildHtmlBody(IReadOnlyList<Order> orders)
    {
        var totalAll = orders.Sum(o => o.TotalAmount);
        var ordersHtml = new StringBuilder();

        foreach (var order in orders)
        {
            var itemsHtml = new StringBuilder();
            foreach (var item in order.Items)
            {
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

            ordersHtml.Append($"""
                <div style="background:#ffffff;border:1px solid #e1dfdd;border-radius:12px;overflow:hidden;margin-bottom:16px;">
                    <div style="padding:16px 20px;background:#0f6cbd;color:#ffffff;font-size:16px;font-weight:700;">
                        Order - {HtmlEncode(order.CustomerEmail)}
                    </div>
                    <div style="padding:16px 20px;">
                        <div style="font-size:14px;color:#605e5c;margin-bottom:4px;">Date (UTC)</div>
                        <div style="font-size:15px;font-weight:600;margin-bottom:14px;">{order.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}</div>
                        <div style="font-size:14px;color:#605e5c;margin-bottom:4px;">Status</div>
                        <div style="font-size:15px;font-weight:600;margin-bottom:14px;">{HtmlEncode(order.Status)}</div>
                        <div style="font-size:15px;font-weight:700;margin-bottom:4px;">Items ({order.Items.Count})</div>
                        <div>{itemsHtml}</div>
                        <div style="display:flex;justify-content:flex-end;padding-top:12px;">
                            <span style="font-size:16px;font-weight:700;color:#1f2328;">Total: {FormatMoney(order.TotalAmount)}</span>
                        </div>
                    </div>
                </div>
                """);
        }

        return $"""
            <!doctype html>
            <html>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:'Segoe UI',Arial,sans-serif;color:#1f2328;">
                <div style="max-width:640px;margin:24px auto;padding:0 12px;">
                    <div style="background:#ffffff;border:1px solid #e1dfdd;border-radius:12px;overflow:hidden;">
                        <div style="padding:16px 20px;background:#0f6cbd;color:#ffffff;font-size:18px;font-weight:700;">Scheduled Order Summary</div>
                        <div style="padding:16px 20px;">
                            <div style="font-size:14px;color:#605e5c;margin-bottom:4px;">Generated at (UTC)</div>
                            <div style="font-size:15px;font-weight:600;margin-bottom:14px;">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</div>
                            <div style="font-size:14px;color:#605e5c;margin-bottom:4px;">Total orders</div>
                            <div style="font-size:15px;font-weight:600;margin-bottom:14px;">{orders.Count}</div>
                            <div style="font-size:15px;font-weight:700;margin-bottom:4px;">Orders</div>
                            <div>{ordersHtml}</div>
                            <div style="display:flex;justify-content:flex-end;padding-top:12px;border-top:1px solid #e0e0e0;margin-top:16px;">
                                <span style="font-size:16px;font-weight:700;color:#1f2328;">Total across all orders: {FormatMoney(totalAll)}</span>
                            </div>
                        </div>
                    </div>
                </div>
            </body>
            </html>
            """
            ;
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
