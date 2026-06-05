using Arabica.Contracts.Olaylar;
using Arabica.Domain.Subeler;

namespace Arabica.Infrastructure.Mesajlasma;

/// <summary>
/// Pure mapping of incoming Kafka events onto branch state. Extracted from the consumer so it is unit-
/// testable without Kafka/DB. NOTE (Slice 2 scope): POS order count is used as a simple occupancy proxy;
/// a richer model (dwell-time coefficient, failover estimation) is future work (blueprint roadmap).
/// </summary>
public static class SubeGuncelleyici
{
    public static void PosUygula(Sube sube, PosOlayi olay)
        => sube.MusteriSayisiniGuncelle(Math.Max(0, olay.SiparisAdedi));

    public static void PdksUygula(Sube sube, PdksOlayi olay)
    {
        var delta = olay.Hareket.Equals("GIRIS", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        sube.AktifPersoneliGuncelle(Math.Max(0, sube.AktifPersonelSayisi + delta));
    }
}
