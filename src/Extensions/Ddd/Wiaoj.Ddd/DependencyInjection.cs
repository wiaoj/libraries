using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using Wiaoj.Ddd;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.Internal;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 
/// <summary>
/// Provides extension methods for setting up DDD (Domain-Driven Design) architecture services and event handlers.
/// </summary>
public static class DependencyInjection {
    extension(IServiceCollection services) {
        /// <summary>
        /// Adds DDD architecture services to the specified <see cref="IServiceCollection"/>.
        /// Initializes the <see cref="IDddBuilder"/> and default dispatcher.
        /// </summary>
        /// <param name="configure">An action to configure the DDD builder (e.g., adding handlers, repositories).</param>
        /// <returns>The <see cref="IDddBuilder"/> instance for further configuration chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
        public IDddBuilder AddDdd(Action<IDddBuilder> configure) {
            Preca.ThrowIfNull(configure);

            // Builder pattern: Ensure builder exists or create a new one.
            if(services.FirstOrDefault(x => x.ServiceType == typeof(IDddBuilder))?.ImplementationInstance is not DddBuilder builder) {
                builder = new DddBuilder(services);
                services.AddSingleton<IDddBuilder>(builder);

                // Register default InMemory dispatcher if no other dispatcher is registered.
                services.TryAddScoped<IDomainEventDispatcher, InMemoryDomainEventDispatcher>();
            }

            configure(builder);
            return builder;
        }
    }

    extension(IDddBuilder builder) {
        #region Pre-Commit Handler Extensions

        /// <summary>
        /// Adds a <see cref="ServiceLifetime.Scoped"/> implementation of <see cref="IPreDomainEventHandler{TDomainEvent}"/>.
        /// Pre-commit handlers are executed before the transaction is committed.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddScopedPreDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent
            where THandler : class, IPreDomainEventHandler<TDomainEvent> {
            return builder.AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Adds a <see cref="ServiceLifetime.Transient"/> implementation of <see cref="IPreDomainEventHandler{TDomainEvent}"/>.
        /// Pre-commit handlers are executed before the transaction is committed.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddTransientPreDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent
            where THandler : class, IPreDomainEventHandler<TDomainEvent> {
            return builder.AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Transient);
        }

        /// <summary>
        /// Adds a <see cref="ServiceLifetime.Singleton"/> implementation of <see cref="IPreDomainEventHandler{TDomainEvent}"/>.
        /// Pre-commit handlers are executed before the transaction is committed.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddSingletonPreDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent
            where THandler : class, IPreDomainEventHandler<TDomainEvent> {
            return builder.AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Singleton);
        }
        #endregion

        #region Post-Commit Handler Extensions

        /// <summary>
        /// Adds a <see cref="ServiceLifetime.Scoped"/> implementation of <see cref="IPostDomainEventHandler{TDomainEvent}"/>.
        /// Post-commit handlers are executed after the transaction has been successfully committed (e.g., via Outbox).
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddScopedPostDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent
            where THandler : class, IPostDomainEventHandler<TDomainEvent> {
            return builder.AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Adds a <see cref="ServiceLifetime.Transient"/> implementation of <see cref="IPostDomainEventHandler{TDomainEvent}"/>.
        /// Post-commit handlers are executed after the transaction has been successfully committed.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddTransientPostDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent
            where THandler : class, IPostDomainEventHandler<TDomainEvent> {
            return builder.AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Transient);
        }

        /// <summary>
        /// Adds a <see cref="ServiceLifetime.Singleton"/> implementation of <see cref="IPostDomainEventHandler{TDomainEvent}"/>.
        /// Post-commit handlers are executed after the transaction has been successfully committed.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddSingletonPostDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent
            where THandler : class, IPostDomainEventHandler<TDomainEvent> {
            return builder.AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Singleton);
        }
        #endregion

        #region Scanning Extensions

        /// <summary>
        /// Scans the assembly containing the specified type <typeparamref name="T"/> for implementations of <see cref="IPreDomainEventHandler{TDomainEvent}"/> 
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <typeparam name="T">A marker type located in the assembly to be scanned.</typeparam>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanPreDomainEventHandlers<T>(ServiceLifetime defaultLifetime) {
            return builder.ScanPreDomainEventHandlers(defaultLifetime, [typeof(T).Assembly]);
        }

        /// <summary>
        /// Scans the specified assemblies for implementations of <see cref="IPreDomainEventHandler{TDomainEvent}"/> 
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <param name="assemblies">The list of assemblies to scan.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assemblies"/> is null.</exception>
        public IDddBuilder ScanPreDomainEventHandlers(ServiceLifetime defaultLifetime, params IEnumerable<Assembly> assemblies) {
            Preca.ThrowIfNull(assemblies);

            // Reflectively find the AddPreDomainEventHandler method to invoke it generically later.
            MethodInfo addMethod = typeof(IDddBuilder).GetMethod(nameof(IDddBuilder.AddPreDomainEventHandler))
                ?? throw new InvalidOperationException($"Method '{nameof(IDddBuilder.AddPreDomainEventHandler)}' not found on IDddBuilder.");

            ScanAndRegisterHandlers(builder, assemblies, typeof(IPreDomainEventHandler<>), addMethod, defaultLifetime);
            return builder;
        }

        /// <summary>
        /// Scans the assembly containing the specified type <typeparamref name="T"/> for implementations of <see cref="IPostDomainEventHandler{TDomainEvent}"/> 
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <typeparam name="T">A marker type located in the assembly to be scanned.</typeparam>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanPostDomainEventHandlers<T>(ServiceLifetime defaultLifetime) {
            return builder.ScanPostDomainEventHandlers(defaultLifetime, [typeof(T).Assembly]);
        }

        /// <summary>
        /// Scans the specified assemblies for implementations of <see cref="IPostDomainEventHandler{TDomainEvent}"/> 
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <param name="assemblies">The list of assemblies to scan.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assemblies"/> is null.</exception>
        public IDddBuilder ScanPostDomainEventHandlers(ServiceLifetime defaultLifetime, params IEnumerable<Assembly> assemblies) {
            Preca.ThrowIfNull(assemblies);

            // Reflectively find the AddPostDomainEventHandler method to invoke it generically later.
            MethodInfo addMethod = typeof(IDddBuilder).GetMethod(nameof(IDddBuilder.AddPostDomainEventHandler))
                 ?? throw new InvalidOperationException($"Method '{nameof(IDddBuilder.AddPostDomainEventHandler)}' not found on IDddBuilder.");

            ScanAndRegisterHandlers(builder, assemblies, typeof(IPostDomainEventHandler<>), addMethod, defaultLifetime);
            return builder;
        }

        /// <summary>
        /// Scans the assembly containing the specified type <typeparamref name="T"/> for both 
        /// <see cref="IPreDomainEventHandler{TDomainEvent}"/> and <see cref="IPostDomainEventHandler{TDomainEvent}"/> 
        /// implementations and registers them.
        /// </summary>
        /// <typeparam name="T">A marker type located in the assembly to be scanned.</typeparam>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanDomainEventHandlers<T>(ServiceLifetime defaultLifetime) {
            return builder.ScanDomainEventHandlers(defaultLifetime, [typeof(T).Assembly]);
        }

        /// <summary>
        /// Scans the specified assemblies for both <see cref="IPreDomainEventHandler{TDomainEvent}"/> 
        /// and <see cref="IPostDomainEventHandler{TDomainEvent}"/> implementations 
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <param name="assemblies">The list of assemblies to scan.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanDomainEventHandlers(ServiceLifetime defaultLifetime, params IEnumerable<Assembly> assemblies) {
            builder.ScanPreDomainEventHandlers(defaultLifetime, assemblies);
            builder.ScanPostDomainEventHandlers(defaultLifetime, assemblies);
            return builder;
        }

        #endregion

        // --- Private Helper to remove code duplication ---
        private static void ScanAndRegisterHandlers(
            IDddBuilder dddBuilder,
            IEnumerable<Assembly> assemblies,
            Type openInterfaceType,
            MethodInfo addMethod,
            ServiceLifetime lifetime) {
            foreach(Assembly assembly in assemblies) {
                // Select only concrete classes (no abstracts, no interfaces)
                IEnumerable<Type> types = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && !t.IsInterface);

                foreach(Type type in types) {
                    // Find interfaces implemented by the type that match the open generic interface
                    IEnumerable<Type> interfaces = type.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterfaceType);

                    foreach(Type handlerInterface in interfaces) {
                        // Extract the Event type from the interface (e.g., IPreDomainEventHandler<UserCreated> -> UserCreated)
                        Type eventType = handlerInterface.GetGenericArguments()[0];

                        // Construct the generic method: AddPreDomainEventHandler<TEvent, THandler>
                        MethodInfo genericMethod = addMethod.MakeGenericMethod(eventType, type);

                        // Invoke the builder method
                        genericMethod.Invoke(dddBuilder, [lifetime]);
                    }
                }
            }
        }
    }
}