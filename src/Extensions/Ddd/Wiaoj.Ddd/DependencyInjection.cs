using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Ddd;
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Abstractions.DomainEvents;
using Wiaoj.Ddd.Internal;

#pragma warning disable IDE0130 
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 
/// <summary>
/// Provides extension methods for setting up DDD (Domain-Driven Design) services.
/// </summary>
public static class DependencyInjection {

    extension(IServiceCollection services) {
        /// <summary>
        /// Adds DDD architecture services to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="configure">An action to configure the DDD builder.</param>
        /// <returns>The <see cref="IDddBuilder"/> instance for further configuration.</returns>
        public IDddBuilder AddDdd(Action<IDddBuilder> configure) {
            if (services.FirstOrDefault(x => x.ServiceType == typeof(IDddBuilder))?.ImplementationInstance is not DddBuilder builder) {
                builder = new DddBuilder(services);
                services.AddSingleton<IDddBuilder>(builder);

                //todo@: change channel if needed
                services.TryAddScoped<IDomainEventDispatcher, InMemoryDomainEventDispatcher>();
            }

            configure(builder);
            return builder;
        }
    }

    extension(IDddBuilder builder) {
        #region Pre-Commit Handler Extensions

        /// <summary>
        /// Adds a scoped <see cref="IPreDomainEventHandler{TDomainEvent}"/> to the service collection.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddScopedPreDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPreDomainEventHandler<TDomainEvent> {
            return builder.AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Adds a transient <see cref="IPreDomainEventHandler{TDomainEvent}"/> to the service collection.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddTransientPreDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPreDomainEventHandler<TDomainEvent> {
            return builder.AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Transient);
        }

        /// <summary>
        /// Adds a singleton <see cref="IPreDomainEventHandler{TDomainEvent}"/> to the service collection.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddSingletonPreDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPreDomainEventHandler<TDomainEvent> {
            return builder.AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Singleton);
        }
        #endregion

        #region Post-Commit Handler Extensions

        /// <summary>
        /// Adds a scoped <see cref="IPostDomainEventHandler{TDomainEvent}"/> to the service collection.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddScopedPostDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPostDomainEventHandler<TDomainEvent> {
            return builder.AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Adds a transient <see cref="IPostDomainEventHandler{TDomainEvent}"/> to the service collection.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddTransientPostDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPostDomainEventHandler<TDomainEvent> {
            return builder.AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Transient);
        }

        /// <summary>
        /// Adds a singleton <see cref="IPostDomainEventHandler{TDomainEvent}"/> to the service collection.
        /// </summary>
        /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
        /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder AddSingletonPostDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPostDomainEventHandler<TDomainEvent> {
            return builder.AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Singleton);
        }
        #endregion

        #region Scanning Extensions

        /// <summary>
        /// Scans the assembly containing the specified type <typeparamref name="T"/> for implementations of <see cref="IPreDomainEventHandler{T}"/> 
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <typeparam name="T">A type located in the assembly to be scanned.</typeparam>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanPreDomainEventHandlers<T>(ServiceLifetime defaultLifetime) {
            return builder.ScanPreDomainEventHandlers(defaultLifetime, [typeof(T).Assembly]);
        }

        /// <summary>
        /// Scans the specified assemblies for implementations of <see cref="IPreDomainEventHandler{T}"/> 
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanPreDomainEventHandlers(ServiceLifetime defaultLifetime, params IEnumerable<Assembly> assemblies) {
            foreach (Assembly assembly in assemblies) {
                IEnumerable<Type> types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface);

                foreach (Type? type in types) {
                    IEnumerable<Type> preHandlerInterfaces = type.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPreDomainEventHandler<>));

                    foreach (Type? handlerInterface in preHandlerInterfaces) {
                        Type eventType = handlerInterface.GetGenericArguments()[0];
                        MethodInfo? method = typeof(IDddBuilder).GetMethod(nameof(IDddBuilder.AddPreDomainEventHandler));
                        MethodInfo genericMethod = method!.MakeGenericMethod(eventType, type);
                        genericMethod.Invoke(builder, [defaultLifetime]);
                    }
                }
            }
            return builder;
        }

        /// <summary>
        /// Scans the assembly containing the specified type <typeparamref name="T"/> for implementations of <see cref="IPostDomainEventHandler{T}"/> 
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <typeparam name="T">A type located in the assembly to be scanned.</typeparam>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanPostDomainEventHandlers<T>(ServiceLifetime defaultLifetime) {
            return builder.ScanPostDomainEventHandlers(defaultLifetime, [typeof(T).Assembly]);
        }

        /// <summary>
        /// Scans the specified assemblies for implementations of <see cref="IPostDomainEventHandler{T}"/> 
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanPostDomainEventHandlers(ServiceLifetime defaultLifetime, params IEnumerable<Assembly> assemblies) {
            foreach (Assembly assembly in assemblies) {
                IEnumerable<Type> types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface);

                foreach (Type? type in types) {
                    IEnumerable<Type> postHandlerInterfaces = type.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPostDomainEventHandler<>));

                    foreach (Type? handlerInterface in postHandlerInterfaces) {
                        Type eventType = handlerInterface.GetGenericArguments()[0];
                        MethodInfo? method = typeof(IDddBuilder).GetMethod(nameof(IDddBuilder.AddPostDomainEventHandler));
                        MethodInfo genericMethod = method!.MakeGenericMethod(eventType, type);
                        genericMethod.Invoke(builder, [defaultLifetime]);
                    }
                }
            }
            return builder;
        }

        /// <summary>
        /// Scans the assembly containing the specified type <typeparamref name="T"/> for both <see cref="IPreDomainEventHandler{T}"/> 
        /// and <see cref="IPostDomainEventHandler{T}"/> implementations and registers them.
        /// </summary>
        /// <typeparam name="T">A type located in the assembly to be scanned.</typeparam>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanDomainEventHandlers<T>(ServiceLifetime defaultLifetime) {
            return builder.ScanDomainEventHandlers(defaultLifetime, [typeof(T).Assembly]);
        }

        /// <summary>
        /// Scans the specified assemblies for both <see cref="IPreDomainEventHandler{T}"/> and <see cref="IPostDomainEventHandler{T}"/> implementations
        /// and registers them with the specified lifetime.
        /// </summary>
        /// <param name="defaultLifetime">The service lifetime to register the handlers with.</param>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>The current <see cref="IDddBuilder"/> instance.</returns>
        public IDddBuilder ScanDomainEventHandlers(ServiceLifetime defaultLifetime, params IEnumerable<Assembly> assemblies) {
            builder.ScanPreDomainEventHandlers(defaultLifetime, assemblies);
            builder.ScanPostDomainEventHandlers(defaultLifetime, assemblies);
            return builder;
        }

        #endregion
    }
}