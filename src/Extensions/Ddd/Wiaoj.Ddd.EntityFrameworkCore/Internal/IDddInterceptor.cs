using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal;
/// <summary>
/// Marker interface for interceptors owned by this package (audit, domain event dispatcher / outbox).
/// Registered in DI under this type so <c>UseDddInterceptors</c> can attach only the DDD interceptors
/// to a context, without sweeping in unrelated <see cref="IInterceptor"/> registrations from the
/// application or other libraries.
/// </summary>
internal interface IDddInterceptor : IInterceptor;
