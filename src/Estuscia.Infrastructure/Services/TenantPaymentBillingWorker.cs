using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Estuscia.Infrastructure.Services;

public class TenantPaymentBillingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TenantPaymentBillingWorker> _logger;

    public TenantPaymentBillingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<TenantPaymentBillingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        /*
         * Run once shortly after application startup.
         */
        await RunBillingCheckAsync(stoppingToken);

        /*
         * Then run once every 24 hours.
         */
        using var timer = new PeriodicTimer(
            TimeSpan.FromHours(24));

        while (
            await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunBillingCheckAsync(stoppingToken);
        }
    }

    private async Task RunBillingCheckAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope =
                _scopeFactory.CreateScope();

            var billingService =
                scope.ServiceProvider
                    .GetRequiredService<
                        TenantPaymentBillingService>();

            await billingService
                .GeneratePendingPaymentsAsync(
                    cancellationToken);

            _logger.LogInformation(
                "Tenant payment billing check completed at {Time}",
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Tenant payment billing check failed.");
        }
    }
}