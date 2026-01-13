using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Ddd;
using Wiaoj.Ddd.EntityFrameworkCore;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure
public static class DependencyInjection {
    /// <param name="builder">The DDD builder.</param>
    extension(IDddBuilder builder) {
        /// <summary>
        /// Registers EF Core interceptors for auditing, domain event processing, and the Outbox pattern using default configuration.
        /// </summary>
        /// <remarks>
        /// This overload uses default <see cref="OutboxOptions"/> and defaults to System.Text.Json for serialization.
        /// </remarks>
        /// <typeparam name="TContext">The concrete <see cref="DbContext"/> type.</typeparam>
        /// <returns>The <see cref="IDddBuilder"/> instance for chaining.</returns>
        public IDddBuilder AddEntityFrameworkCore<TContext>() where TContext : DbContext {
            return builder.AddEntityFrameworkCore<TContext>(_ => { });
        }

        /// <summary>
        /// Registers EF Core interceptors for auditing, domain event processing, and the Outbox pattern with custom configuration.
        /// </summary>
        /// <typeparam name="TContext">The concrete <see cref="DbContext"/> type.</typeparam>
        /// <param name="configure">A builder action to configure <see cref="OutboxOptions"/> and the serializer.</param>
        /// <returns>The <see cref="IDddBuilder"/> instance for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configure"/> is null.</exception>
        public IDddBuilder AddEntityFrameworkCore<TContext>(Action<DddEfCoreOptionsBuilder> configure)
            where TContext : DbContext {
            Preca.ThrowIfNull(configure);

            builder.Services.TryAddSingleton<OutboxChannel>();
            builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
            builder.Services.TryAddSingleton<OutboxInstanceInfo>();

            builder.Services.TryAddScoped<AuditInterceptor>();
            builder.Services.TryAddScoped<DomainEventDispatcherInterceptor>();

            DddEfCoreOptionsBuilder optionsBuilder = new(builder.Services);

            configure.Invoke(optionsBuilder);

            optionsBuilder.Build();

            builder.Services.AddHostedService<OutboxProcessor<TContext>>();

            return builder;
        }

        /// <summary>
        /// Registers the DbContext as the IUnitOfWork interface using a Scoped lifetime.
        /// This allows the DbContext to serve as the transaction manager (UoW) for the scope.
        /// </summary>
        /// <typeparam name="TContext">The DbContext type which must implement IUnitOfWork.</typeparam>
        /// <param name="builder">The IDddBuilder instance.</param>
        /// <returns>The IDddBuilder instance for chaining.</returns>
        public IDddBuilder AddUnitOfWork<TContext>() where TContext : DbContext, IUnitOfWork {
            return AddUnitOfWork<TContext>(builder, ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Registers the DbContext as the IUnitOfWork interface with a specified service lifetime.
        /// </summary>
        /// <typeparam name="TContext">The DbContext type which must implement IUnitOfWork.</typeparam>
        /// <param name="builder">The IDddBuilder instance.</param>
        /// <param name="lifetime">The ServiceLifetime (Scoped, Transient, Singleton) to register the service with.</param>
        /// <returns>The IDddBuilder instance for chaining.</returns>
        public IDddBuilder AddUnitOfWork<TContext>(ServiceLifetime lifetime) where TContext : DbContext, IUnitOfWork {
            ServiceDescriptor descriptor = new(
                typeof(IUnitOfWork),
                provider => provider.GetRequiredService<TContext>(),
                lifetime
            );
            builder.Services.TryAdd(descriptor);
            return builder;
        }

        /// <summary>
        /// Registers a specific Repository interface and implementation pair using all explicit generic types (TAggregate, TId, TContext).
        /// Defaults to Scoped lifetime.
        /// </summary>
        public IDddBuilder AddRepository<TRepository, TImplementation, TAggregate, TId, TContext>()
            where TRepository : class, IRepository<TAggregate, TId>
            where TImplementation : EfcoreRepository<TContext, TAggregate, TId>, TRepository
            where TAggregate : class, IAggregate
            where TId : notnull
            where TContext : DbContext {
            return AddRepository<TRepository, TImplementation, TAggregate, TId, TContext>(builder, ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Registers a specific Repository interface and implementation pair using all explicit generic types (TAggregate, TId, TContext)
        /// with a specified service lifetime. This method enforces strong compile-time checks on the DDD structure.
        /// </summary>
        public IDddBuilder AddRepository<TRepository, TImplementation, TAggregate, TId, TContext>(ServiceLifetime lifetime)
            where TRepository : class, IRepository<TAggregate, TId>
            where TImplementation : EfcoreRepository<TContext, TAggregate, TId>, TRepository
            where TAggregate : class, IAggregate
            where TId : notnull
            where TContext : DbContext {
            builder.Services.TryAdd(ServiceDescriptor.Describe(typeof(TRepository), typeof(TImplementation), lifetime));
            return builder;
        }

        /// <summary>
        /// Registers a Repository pair by inferring TContext, TAggregate, and TId at runtime 
        /// using Reflection on the TImplementation's inheritance chain. Defaults to Scoped lifetime.
        /// </summary>
        public IDddBuilder AddRepository<TRepository, TImplementation>()
            where TRepository : class, IRepositoryMarker
            where TImplementation : class, IEfcoreRepository, TRepository {
            return AddRepository<TRepository, TImplementation>(builder, ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Registers a Repository pair by inferring TContext, TAggregate, and TId at runtime 
        /// using Reflection with a specified service lifetime.
        /// </summary>
        public IDddBuilder AddRepository<TRepository, TImplementation>(ServiceLifetime lifetime)
            where TRepository : class, IRepositoryMarker
            where TImplementation : class, IEfcoreRepository, TRepository {
            Type implementationType = typeof(TImplementation);
            Type? baseType = implementationType.BaseType;

            if (baseType == null || !baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(EfcoreRepository<,,>)) {
                throw new InvalidOperationException(
                    $"Error: '{implementationType.Name}' must inherit from 'EfcoreRepository<TContext, TAggregate, TId>'. The base type check failed."
                );
            }

            builder.Services.TryAdd(ServiceDescriptor.Describe(typeof(TRepository), implementationType, lifetime));
            return builder;
        }

        /// <summary>
        /// Scans a collection of Assemblies for concrete Repository implementations (classes implementing IEfcoreRepository)
        /// and registers them against their corresponding Repository interfaces (implementing IRepositoryMarker).
        /// Defaults to Scoped lifetime.
        /// </summary>
        /// <typeparam name="TContext">The common DbContext used by all repositories in the scanned assemblies.</typeparam>
        /// <param name="assemblies">The assemblies to scan for Repository types.</param>
        public IDddBuilder AddRepositoriesFromAssemblies<TContext>(params IEnumerable<Assembly> assemblies)
            where TContext : DbContext {
            return AddRepositoriesFromAssemblies<TContext>(builder, ServiceLifetime.Scoped, assemblies);
        }

        /// <summary>
        /// Scans a collection of Assemblies for concrete Repository implementations (classes implementing IEfcoreRepository)
        /// and registers them against their corresponding Repository interfaces (implementing IRepositoryMarker)
        /// with a specified service lifetime.
        /// </summary>
        public IDddBuilder AddRepositoriesFromAssemblies<TContext>(ServiceLifetime lifetime, params IEnumerable<Assembly> assemblies)
            where TContext : DbContext {
            Preca.ThrowIfNull(assemblies);
            Preca.ThrowIfFalse(assemblies.Any());

            Type repositoryInterfaceType = typeof(IRepositoryMarker);
            Type implementationMarkerType = typeof(IEfcoreRepository);

            IEnumerable<Type> allTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition);

            foreach (Type? implementationType in allTypes) {
                if (!implementationType.IsAssignableTo(implementationMarkerType)) {
                    continue;
                }

                IEnumerable<Type> repositoryInterfaces = implementationType
                    .GetInterfaces()
                    .Where(i => i.IsAssignableTo(repositoryInterfaceType) && i != repositoryInterfaceType);

                foreach (Type? repositoryInterface in repositoryInterfaces) {
                    builder.Services.TryAdd(ServiceDescriptor.Describe(repositoryInterface, implementationType, lifetime));
                }
            }

            return builder;
        }

        /// <summary>
        /// Scans the Assembly containing the type TMarker for all Repository implementations.
        /// Defaults to Scoped lifetime.
        /// </summary>
        /// <typeparam name="TContext">The common DbContext type.</typeparam>
        /// <typeparam name="TMarker">A type whose Assembly will be scanned (e.g., a Repository or an Aggregate).</typeparam>
        public IDddBuilder AddRepositoriesFromAssemblyContaining<TContext, TMarker>() where TContext : DbContext {
            return AddRepositoriesFromAssemblies<TContext>(builder, ServiceLifetime.Scoped, [typeof(TMarker).Assembly]);
        }

        /// <summary>
        /// Scans the Assembly containing the type TMarker for all Repository implementations 
        /// with a specified service lifetime.
        /// </summary>
        /// <typeparam name="TContext">The common DbContext type.</typeparam>
        /// <typeparam name="TMarker">A type whose Assembly will be scanned (e.g., a Repository or an Aggregate).</typeparam>
        /// <param name="lifetime">The ServiceLifetime to register the services with.</param>
        public IDddBuilder AddRepositoriesFromAssemblyContaining<TContext, TMarker>(ServiceLifetime lifetime) where TContext : DbContext {
            return AddRepositoriesFromAssemblies<TContext>(builder, lifetime, [typeof(TMarker).Assembly]);
        }
    }

    /// <param name="optionsBuilder">The DbContext options builder.</param>
    extension(DbContextOptionsBuilder optionsBuilder) {
        /// <summary>
        /// Registers all necessary DDD interceptors (like AuditInterceptor and DomainEventDispatcherInterceptor)
        /// from the service provider. This is the recommended way to add DDD capabilities to your DbContext.
        /// This method is safe to use with Scoped interceptors when used with AddDbContext.
        /// </summary>
        /// <param name="serviceProvider">The scoped service provider, passed from the AddDbContext delegate.</param>
        /// <returns>The options builder for chaining.</returns>
        public DbContextOptionsBuilder UseDddInterceptors(IServiceProvider serviceProvider) {
            return optionsBuilder.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<DomainEventDispatcherInterceptor>());
        }
    }
}