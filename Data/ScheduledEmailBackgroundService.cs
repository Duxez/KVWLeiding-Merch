using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace kvwleidingmerch.Data;

public sealed class ScheduledEmailBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledEmailBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public ScheduledEmailBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledEmailBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduled Email Background Service started. Checking every {Interval} minutes.", _checkInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendPendingEmailsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Scheduled Email Background Service: {Message}", ex.Message);
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Scheduled Email Background Service is stopping.");
    }

    private async Task CheckAndSendPendingEmailsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmailSchedulerService>();
        await service.CheckAndSendPendingEmailsAsync(cancellationToken);
    }
}
