using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Arabica.Infrastructure.Cikti;

/// <summary>
/// Hosted wrapper that polls the outbox on an interval and publishes pending notifications via
/// <see cref="OutboxGonderici"/>. A new DI scope per tick gives a correctly-scoped history DbContext.
/// </summary>
public sealed class OutboxDispatcherServisi(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherServisi> log) : BackgroundService
{
    private static readonly TimeSpan Aralik = TimeSpan.FromMilliseconds(500);
    private const int ParutiBoyutu = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Outbox dispatcher başladı (aralık: {Aralik} ms).", Aralik.TotalMilliseconds);
        using var zamanlayici = new PeriodicTimer(Aralik);

        while (await zamanlayici.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var gonderici = scope.ServiceProvider.GetRequiredService<OutboxGonderici>();
                await gonderici.BirPartiGonderAsync(ParutiBoyutu, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Outbox gönderim döngüsünde hata; bir sonraki turda yeniden denenecek.");
            }
        }
    }
}
