using Arabica.Application.Denetim;
using Arabica.Application.Ortak;
using Arabica.Contracts.Api;
using Arabica.Domain.Subeler;
using FluentValidation;
using MediatR;

namespace Arabica.Application.Yonetim;

// Branch admin (hot schema, low-frequency config writes). Handlers self-commit the hot context; the hist
// TransactionBehavior runs as a no-op. Each mutation is audited (actor + IP + timestamp).

// ---- create ----
public sealed record SubeOlusturCommand(string Ad, int MaksimumKapasite, int AktifPersonelSayisi) : IKomut<SubeYonetimYaniti>;

public sealed class SubeOlusturCommandValidator : AbstractValidator<SubeOlusturCommand>
{
    public SubeOlusturCommandValidator()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaksimumKapasite).GreaterThan(0);
        RuleFor(x => x.AktifPersonelSayisi).GreaterThanOrEqualTo(0);
    }
}

public sealed class SubeOlusturCommandHandler(ISubeRepository repo, IDenetimYazici denetim)
    : IRequestHandler<SubeOlusturCommand, SubeYonetimYaniti>
{
    public async Task<SubeYonetimYaniti> Handle(SubeOlusturCommand k, CancellationToken ct)
    {
        var mevcut = await repo.TumunuGetirAsync(ct);
        var yeniId = mevcut.Count == 0 ? 1 : mevcut.Max(s => s.SubeId) + 1; // SubeId DB-generated değil
        var sube = new Sube(yeniId, k.Ad, k.MaksimumKapasite, anlikMusteriSayisi: 0, aktifPersonelSayisi: k.AktifPersonelSayisi);
        await repo.EkleAsync(sube, ct);
        await repo.KaydetAsync(ct);
        await denetim.YazAsync("ADMIN:SubeOlustur", $"şube {yeniId} '{k.Ad}'", ct);
        return Map(sube);
    }

    internal static SubeYonetimYaniti Map(Sube s)
        => new(s.SubeId, s.Ad, s.MaksimumKapasite, s.AnlikMusteriSayisi, s.AktifPersonelSayisi, s.Aktif);
}

// ---- update ----
public sealed record SubeGuncelleCommand(int SubeId, string Ad, int MaksimumKapasite, int AktifPersonelSayisi) : IKomut<SubeYonetimYaniti?>;

public sealed class SubeGuncelleCommandValidator : AbstractValidator<SubeGuncelleCommand>
{
    public SubeGuncelleCommandValidator()
    {
        RuleFor(x => x.SubeId).GreaterThan(0);
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaksimumKapasite).GreaterThan(0);
        RuleFor(x => x.AktifPersonelSayisi).GreaterThanOrEqualTo(0);
    }
}

public sealed class SubeGuncelleCommandHandler(ISubeRepository repo, IDenetimYazici denetim)
    : IRequestHandler<SubeGuncelleCommand, SubeYonetimYaniti?>
{
    public async Task<SubeYonetimYaniti?> Handle(SubeGuncelleCommand k, CancellationToken ct)
    {
        var sube = await repo.GetirAsync(k.SubeId, ct);
        if (sube is null) return null;
        sube.Guncelle(k.Ad, k.MaksimumKapasite, k.AktifPersonelSayisi);
        await repo.KaydetAsync(ct);
        await denetim.YazAsync("ADMIN:SubeGuncelle", $"şube {k.SubeId}", ct);
        return SubeOlusturCommandHandler.Map(sube);
    }
}

// ---- soft-deactivate ----
public sealed record SubePasiflestirCommand(int SubeId) : IKomut<SubeYonetimYaniti?>;

public sealed class SubePasiflestirCommandHandler(ISubeRepository repo, IDenetimYazici denetim)
    : IRequestHandler<SubePasiflestirCommand, SubeYonetimYaniti?>
{
    public async Task<SubeYonetimYaniti?> Handle(SubePasiflestirCommand k, CancellationToken ct)
    {
        var sube = await repo.GetirAsync(k.SubeId, ct);
        if (sube is null) return null;
        sube.Pasiflestir();
        await repo.KaydetAsync(ct);
        await denetim.YazAsync("ADMIN:SubePasiflestir", $"şube {k.SubeId} pasifleştirildi", ct);
        return SubeOlusturCommandHandler.Map(sube);
    }
}

// ---- reactivate (undo soft-deactivate) ----
public sealed record SubeAktiflestirCommand(int SubeId) : IKomut<SubeYonetimYaniti?>;

public sealed class SubeAktiflestirCommandHandler(ISubeRepository repo, IDenetimYazici denetim)
    : IRequestHandler<SubeAktiflestirCommand, SubeYonetimYaniti?>
{
    public async Task<SubeYonetimYaniti?> Handle(SubeAktiflestirCommand k, CancellationToken ct)
    {
        // GetirAsync loads by id WITHOUT the active-only filter, so inactive branches are reachable here
        // (AktifleriGetirAsync would exclude them). Null → 404 at the controller.
        var sube = await repo.GetirAsync(k.SubeId, ct);
        if (sube is null) return null;
        sube.Aktiflestir();
        await repo.KaydetAsync(ct);
        await denetim.YazAsync("ADMIN:SubeAktiflestir", $"şube {k.SubeId} aktifleştirildi", ct);
        return SubeOlusturCommandHandler.Map(sube);
    }
}

// ---- list (admin: all incl. inactive) ----
public sealed record SubeYonetimListesiQuery : ISorgu<IReadOnlyList<SubeYonetimYaniti>>;

public sealed class SubeYonetimListesiQueryHandler(ISubeRepository repo)
    : IRequestHandler<SubeYonetimListesiQuery, IReadOnlyList<SubeYonetimYaniti>>
{
    public async Task<IReadOnlyList<SubeYonetimYaniti>> Handle(SubeYonetimListesiQuery q, CancellationToken ct)
        => (await repo.TumunuGetirAsync(ct)).Select(SubeOlusturCommandHandler.Map).ToList();
}
