using Arabica.Application.Davranislar;
using Arabica.Application.Ortak;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Arabica.Application.Tests;

/// <summary>
/// The transactional Decorator behavior. Commits exactly once AFTER a successful command, and NEVER when
/// the handler throws — the precondition for "DB + bus cannot diverge".
/// </summary>
public sealed class TransactionBehaviorTests
{
    private sealed record SahteKomut : IKomut<string>;

    private static TransactionBehavior<SahteKomut, string> Davranis(SahteBirimIsi birimIsi)
        => new(birimIsi, NullLogger<TransactionBehavior<SahteKomut, string>>.Instance);

    [Fact]
    public async Task Basarili_handler_da_tam_bir_kez_commit_eder()
    {
        var birimIsi = new SahteBirimIsi();
        var davranis = Davranis(birimIsi);

        var yanit = await davranis.Handle(new SahteKomut(), () => Task.FromResult("tamam"), CancellationToken.None);

        yanit.Should().Be("tamam");
        birimIsi.KaydetSayisi.Should().Be(1);
    }

    [Fact]
    public async Task Handler_firlatirsa_commit_etmez()
    {
        var birimIsi = new SahteBirimIsi();
        var davranis = Davranis(birimIsi);
        RequestHandlerDelegate<string> patlayan = () => throw new InvalidOperationException("geçersiz geçiş");

        var eylem = async () => await davranis.Handle(new SahteKomut(), patlayan, CancellationToken.None);

        await eylem.Should().ThrowAsync<InvalidOperationException>();
        birimIsi.KaydetSayisi.Should().Be(0);
    }
}
