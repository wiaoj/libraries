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

public static class DependencyInjection {
    extension(IServiceCollection services) {
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
        public IDddBuilder AddScopedPreDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPreDomainEventHandler<TDomainEvent> {
            return builder.AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Scoped);
        }

        public IDddBuilder AddTransientPreDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPreDomainEventHandler<TDomainEvent> {
            return builder.AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Transient);
        }

        public IDddBuilder AddSingletonPreDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPreDomainEventHandler<TDomainEvent> {
            return builder.AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Singleton);
        }
        #endregion

        #region Post-Commit Handler Extensions
        public IDddBuilder AddScopedPostDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPostDomainEventHandler<TDomainEvent> {
            return builder.AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Scoped);
        }

        public IDddBuilder AddTransientPostDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPostDomainEventHandler<TDomainEvent> {
            return builder.AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Transient);
        }

        public IDddBuilder AddSingletonPostDomainEventHandler<TDomainEvent, THandler>()
            where TDomainEvent : IDomainEvent where THandler : class, IPostDomainEventHandler<TDomainEvent> {
            return builder.AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime.Singleton);
        }
        #endregion

        // ScanAssemblies metodunu daha esnek hale getirelim
        public IDddBuilder ScanAssemblies(ServiceLifetime defaultLifetime, params IEnumerable<Assembly> assemblies) {
            foreach (Assembly assembly in assemblies) {
                IEnumerable<Type> types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface);

                foreach (Type? type in types) {
                    // Pre-commit handler arayüzlerini bul ve kaydet
                    IEnumerable<Type> preHandlerInterfaces = type.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPreDomainEventHandler<>));

                    foreach (Type? handlerInterface in preHandlerInterfaces) {
                        Type eventType = handlerInterface.GetGenericArguments()[0];
                        MethodInfo? method = typeof(IDddBuilder).GetMethod(nameof(IDddBuilder.AddPreDomainEventHandler));
                        MethodInfo genericMethod = method!.MakeGenericMethod(eventType, type);
                        genericMethod.Invoke(builder, [defaultLifetime]);
                    }

                    // Post-commit handler arayüzlerini bul ve kaydet
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
    }
}