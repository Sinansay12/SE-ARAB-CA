using Arabica.Application.Denetim;
using Arabica.Application.Ortak;
using MediatR;

namespace Arabica.Application.Tohumlama;

/// <summary>Outcome of a demo-seed run (counts of the snapshot that now exists).</summary>
public sealed record DemoTohumSonucu(
    bool Tohumlandi,
    int SubeSayisi,
    int PersonelSayisi,
    int TransferSayisi,
    int BekleyenTransfer,
    int DenetimSayisi);

/// <summary>
/// Demo-only rich dataset seeder. Branches/personnel → <c>hot</c>, transfers/audit → <c>hist</c> (schema
/// isolation honored; no DDL — Liquibase owns the schema). KVKK: personnel carry only a handle + numeric IDs.
/// Implemented in Infrastructure; only runs against a relational store (the InMemory test store keeps the
/// minimal deterministic seed the API tests rely on).
/// </summary>
public interface IDemoVeriTohumlayici
{
    /// <summary>
    /// Seeds the rich snapshot. Idempotent: a no-op when data already exists, UNLESS <paramref name="sifirla"/>
    /// is true (DEMO ONLY) — then the demo tables are cleared first and re-seeded. Returns counts; a no-op run
    /// returns <c>Tohumlandi=false</c>.
    /// </summary>
    Task<DemoTohumSonucu> TohumlaAsync(bool sifirla, CancellationToken ct);
}

/// <summary>
/// CQRS command behind <c>POST /api/v1/admin/seed</c> (Koordinatör; demo-only). Delegates to the seeder and
/// records the action in the audit log.
/// </summary>
public sealed record DemoTohumlaCommand(bool Sifirla) : IKomut<DemoTohumSonucu>;

public sealed class DemoTohumlaCommandHandler(IDemoVeriTohumlayici tohumlayici, IDenetimYazici denetim)
    : IRequestHandler<DemoTohumlaCommand, DemoTohumSonucu>
{
    public async Task<DemoTohumSonucu> Handle(DemoTohumlaCommand k, CancellationToken ct)
    {
        var sonuc = await tohumlayici.TohumlaAsync(k.Sifirla, ct);
        await denetim.YazAsync(
            k.Sifirla ? "ADMIN:DemoVeriSifirla" : "ADMIN:DemoVeriTohumla",
            $"tohumlandı={sonuc.Tohumlandi} şube={sonuc.SubeSayisi} personel={sonuc.PersonelSayisi} transfer={sonuc.TransferSayisi}",
            ct);
        return sonuc;
    }
}
