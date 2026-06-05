using MediatR;

namespace Arabica.Application.Ortak;

/// <summary>CQRS write marker. Commands mutate state (HistoryDbContext + outbox) and are wrapped by the
/// transactional pipeline behavior.</summary>
public interface IKomut<out TYanit> : IRequest<TYanit>;

/// <summary>CQRS read marker. Queries never mutate; they read the hot model (HotDbContext) — no transaction.</summary>
public interface ISorgu<out TYanit> : IRequest<TYanit>;
