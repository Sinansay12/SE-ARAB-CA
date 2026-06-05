using Arabica.Application.Davranislar;
using Arabica.Application.Fasad;
using Arabica.Domain.IsHukuku;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Arabica.Application.Kurulum;

/// <summary>DI registration for the application layer: MediatR (CQRS), the Decorator pipeline, validators,
/// the Facade, and the pure domain services it composes.</summary>
public static class ApplicationKurulum
{
    public static IServiceCollection ArabicaApplicationEkle(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationKurulum).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // DECORATOR chain (outer → inner). Transaction is constrained to IKomut<> so it wraps commands only.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        // Pure domain services (no DI attributes) composed by the Facade.
        services.AddSingleton<ITransferEmriFactory, TransferEmriFactory>();
        services.AddSingleton(_ => IsKanunuDegerlendirici.Varsayilan());
        services.AddSingleton(_ => DolulukEsikleri.Varsayilan);

        services.AddScoped<IKaynakYonetimFasadi, KaynakYonetimFasadi>();

        return services;
    }
}
