namespace Wiaoj.Modulith.Internal;
/// <summary>
/// Topological sort (Kahn's algorithm) for module dependency ordering.
/// Guarantees every module is booted after all its declared dependencies.
/// Throws <see cref="InvalidOperationException"/> on cycles.
/// </summary>
internal static class TopologicalSorter { 
    /// <summary>
    /// Sorts <paramref name="descriptors"/> so that each module appears
    /// only after all its dependencies in the returned list.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a dependency cycle is detected.
    /// </exception>
    public static IReadOnlyList<ModuleDescriptor> Sort(IReadOnlyList<ModuleDescriptor> descriptors) {
        Dictionary<Type, ModuleDescriptor> byType =
            descriptors.ToDictionary(d => d.Type);

        // Validate all declared dependencies exist in the set
        foreach(ModuleDescriptor descriptor in descriptors) {
            foreach(Type dep in descriptor.Dependencies) {
                if(!byType.ContainsKey(dep))
                    throw new InvalidOperationException(
                        $"Module '{descriptor.Type.Name}' declares a dependency on " +
                        $"'{dep.Name}', but that module is not registered.");
            }
        }

        // Build in-degree map
        Dictionary<Type, int> inDegree = descriptors.ToDictionary(d => d.Type, _ => 0);

        foreach(ModuleDescriptor descriptor in descriptors) {
            foreach(Type dep in descriptor.Dependencies) {
                inDegree[descriptor.Type]++;
            }
        }

        // Build adjacency list (dependency → dependents)
        Dictionary<Type, List<Type>> dependents = descriptors.ToDictionary(
            d => d.Type, _ => new List<Type>());

        foreach(ModuleDescriptor descriptor in descriptors) {
            foreach(Type dep in descriptor.Dependencies) {
                dependents[dep].Add(descriptor.Type);
            }
        }

        // Kahn's BFS
        Queue<Type> queue = new(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        List<ModuleDescriptor> sorted = new(descriptors.Count);

        while(queue.Count > 0) {
            Type current = queue.Dequeue();
            sorted.Add(byType[current]);

            foreach(Type dependent in dependents[current]) {
                inDegree[dependent]--;
                if(inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        if(sorted.Count != descriptors.Count) {
            IEnumerable<string> cyclic = inDegree
                .Where(kv => kv.Value > 0)
                .Select(kv => kv.Key.Name);

            throw new InvalidOperationException(
                $"Circular module dependency detected among: {string.Join(", ", cyclic)}. " +
                "Review [DependsOn] attributes to break the cycle.");
        }

        return sorted;
    }
}