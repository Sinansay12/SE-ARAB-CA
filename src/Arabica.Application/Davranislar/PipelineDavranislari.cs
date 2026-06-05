using System.Diagnostics;
using Arabica.Application.Ortak;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Arabica.Application.Davranislar;

// DECORATOR (structural pattern), realized as MediatR IPipelineBehavior. Each behavior wraps the next,
// adding a cross-cutting concern without modifying the handler. Outer→inner order is set at registration:
//   Logging → Validation → Transaction(commands only) → handler.

/// <summary>Logs each request name and elapsed time.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var ad = typeof(TRequest).Name;
        var saat = Stopwatch.StartNew();
        log.LogInformation("→ {Istek} işleniyor", ad);
        try
        {
            var yanit = await next();
            log.LogInformation("✓ {Istek} tamamlandı ({Ms} ms)", ad, saat.ElapsedMilliseconds);
            return yanit;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "✗ {Istek} hata verdi ({Ms} ms)", ad, saat.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>Runs all FluentValidation validators for the request; throws <see cref="ValidationException"/> on failure.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var sonuclar = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
            var hatalar = sonuclar.SelectMany(r => r.Errors).Where(e => e is not null).ToList();
            if (hatalar.Count != 0)
                throw new ValidationException(hatalar);
        }
        return await next();
    }
}

/// <summary>
/// Commits the unit of work exactly once, AFTER the command handler succeeds. The handler mutates the
/// tracked entity and enqueues the outbox row; this single <c>KaydetAsync</c> (one SaveChanges) commits
/// the UPDATE and the outbox INSERT atomically. If the handler throws (e.g. illegal transition), commit is
/// never reached → nothing persists. Applies to COMMANDS only via the <see cref="IKomut{T}"/> constraint.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IBirimIsi birimIsi, ILogger<TransactionBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IKomut<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var yanit = await next();                 // handler: mutate + enqueue outbox (no save)
        var degisen = await birimIsi.KaydetAsync(ct); // single atomic commit
        log.LogInformation("Komut commit edildi: {Komut} ({Degisen} kayıt)", typeof(TRequest).Name, degisen);
        return yanit;
    }
}
