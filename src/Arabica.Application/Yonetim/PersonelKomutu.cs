using Arabica.Application.Denetim;
using Arabica.Application.Ortak;
using Arabica.Contracts.Api;
using FluentValidation;
using MediatR;

namespace Arabica.Application.Yonetim;

/// <summary>
/// Add an anonymized barista (KVKK): the command/DTO carry ONLY TakmaAd + numeric IDs — there is NO field
/// for TC/name/phone anywhere in the type. Persists to hot.personel and bumps the branch's active-staff count.
/// </summary>
public sealed record PersonelEkleCommand(int SubeId, string TakmaAd, string Tip) : IKomut<PersonelYaniti?>;

public sealed class PersonelEkleCommandValidator : AbstractValidator<PersonelEkleCommand>
{
    public PersonelEkleCommandValidator()
    {
        RuleFor(x => x.SubeId).GreaterThan(0);
        RuleFor(x => x.TakmaAd).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Tip).NotEmpty();
    }
}

public sealed class PersonelEkleCommandHandler(
    ISubeRepository subeRepo, IPersonelDeposu personelDeposu, IDenetimYazici denetim)
    : IRequestHandler<PersonelEkleCommand, PersonelYaniti?>
{
    public async Task<PersonelYaniti?> Handle(PersonelEkleCommand k, CancellationToken ct)
    {
        var sube = await subeRepo.GetirAsync(k.SubeId, ct);
        if (sube is null || !sube.Aktif) return null;

        var kayit = new PersonelKaydi { SubeId = k.SubeId, TakmaAd = k.TakmaAd, Tip = k.Tip, Aktif = true };
        await personelDeposu.EkleAsync(kayit, ct);
        sube.AktifPersoneliGuncelle(sube.AktifPersonelSayisi + 1);
        await personelDeposu.KaydetAsync(ct); // hot context (personel + sube aynı bağlamda)

        await denetim.YazAsync("ADMIN:PersonelEkle", $"şube {k.SubeId} takmaAd '{k.TakmaAd}'", ct);
        return new PersonelYaniti(kayit.PersonelId, kayit.SubeId, kayit.TakmaAd, kayit.Tip);
    }
}
