using System.Reflection;

namespace Wiaoj.Modulith.Internal;
internal sealed class ModulithBuilder : IModulithBuilder {

    private readonly List<Type> _candidateTypes = [];

    public IReadOnlyList<Type> CandidateTypes => this._candidateTypes;

    public IModulithBuilder AddModulesFromAssemblyContaining<TMarker>() {
        return AddModulesFromAssembly(typeof(TMarker).Assembly);
    }

    public IModulithBuilder AddModulesFromAssembly(Assembly assembly) {
        IEnumerable<Type> types = assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IModule).IsAssignableFrom(t));

        this._candidateTypes.AddRange(types);
        return this;
    }

    public IModulithBuilder AddModule<TModule>() where TModule : IModule {
        return AddModule(typeof(TModule));
    }

    public IModulithBuilder AddModule(Type moduleType) {
        ArgumentNullException.ThrowIfNull(moduleType);

        if(!typeof(IModule).IsAssignableFrom(moduleType))
            throw new ArgumentException(
                $"'{moduleType.Name}' does not implement IModule.", nameof(moduleType));

        this._candidateTypes.Add(moduleType);
        return this;
    }

    public bool IsRegistered(Type moduleType) {
        return this._candidateTypes.Contains(moduleType);
    }
}