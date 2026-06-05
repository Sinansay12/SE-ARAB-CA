using Arabica.Contracts.Olaylar;
using Arabica.Infrastructure.Mesajlasma;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Arabica.Integration.Tests;

/// <summary>
/// Real-Kafka tests (Testcontainers). Gated on Docker. Verifies the producer publishes and a consumer
/// receives the exact payload (round-trip), keyed by SubeId.
/// </summary>
public sealed class KafkaEntegrasyonTests(KafkaFixture fixture) : IClassFixture<KafkaFixture>
{
    [SkippableFact]
    public async Task Uretici_yayinlar_tuketici_alir_round_trip()
    {
        Skip.IfNot(fixture.Kullanilabilir, fixture.AtlamaSebebi);

        var ayar = Options.Create(new KafkaSecenekleri { BootstrapSunuculari = fixture.BootstrapSunuculari });
        using var uretici = new KafkaUreticisi(ayar);

        var olay = new PosOlayi(SubeId: 1, SiparisAdedi: 42, ToplamTutar: 123.45m, UretimZamani: DateTimeOffset.UtcNow);
        await uretici.YayinlaAsync(Topicler.Pos, "1", olay, CancellationToken.None);

        var config = new ConsumerConfig
        {
            BootstrapServers = fixture.BootstrapSunuculari,
            GroupId = "test-" + Guid.NewGuid().ToString("N"),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        using var tuketici = new ConsumerBuilder<string, string>(config).Build();
        tuketici.Subscribe(Topicler.Pos);

        var sonuc = tuketici.Consume(TimeSpan.FromSeconds(20));
        tuketici.Close();

        sonuc.Should().NotBeNull();
        sonuc!.Message.Key.Should().Be("1");
        var alinan = System.Text.Json.JsonSerializer.Deserialize<PosOlayi>(
            sonuc.Message.Value, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        alinan.Should().NotBeNull();
        alinan!.SubeId.Should().Be(1);
        alinan.SiparisAdedi.Should().Be(42);
    }
}
