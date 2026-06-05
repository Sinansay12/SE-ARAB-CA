using Arabica.Application.Cikti;
using Arabica.Application.Ortak;
using Arabica.Domain.Transferler;

namespace Arabica.Application.Tests;

// Hand-written test doubles (fakes) — record interactions so behavior can be asserted without infra.

internal sealed class SahteTransferEmriDeposu(TransferEmri? emir) : ITransferEmriRepository
{
    public Task<TransferEmri?> GetirAsync(long emirId, CancellationToken ct) => Task.FromResult(emir);
    public Task EkleAsync(TransferEmri e, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<TransferEmri>> BekleyenleriGetirAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<TransferEmri>>([]);
    public Task<IReadOnlyList<TransferEmri>> GecmisGetirAsync(int enFazla, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<TransferEmri>>([]);
}

internal sealed class SahteOutbox : IOutbox
{
    public List<object> Olaylar { get; } = [];
    public void Ekle(object entegrasyonOlayi, string anahtar, DateTimeOffset an) => Olaylar.Add(entegrasyonOlayi);
}

internal sealed class SahteBirimIsi : IBirimIsi
{
    public int KaydetSayisi { get; private set; }
    public Task<int> KaydetAsync(CancellationToken ct)
    {
        KaydetSayisi++;
        return Task.FromResult(1);
    }
}

internal sealed class SabitZaman(DateTimeOffset an) : IZamanSaglayici
{
    public DateTimeOffset Simdi { get; } = an;
}
