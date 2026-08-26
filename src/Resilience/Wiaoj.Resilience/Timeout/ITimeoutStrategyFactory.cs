namespace Wiaoj.Resilience;

/// <summary>
/// Factory contract for creating and resolving configured <see cref="ITimeoutStrategy"/> instances.
/// </summary>
public interface ITimeoutStrategyFactory {
    /// <summary>Creates or resolves a timeout strategy configured for the strongly-typed policy tag.</summary>
    ITimeoutStrategy Create<TPolicy>() where TPolicy : notnull;

    /// <summary>Creates or resolves a timeout strategy by policy name.</summary>
    ITimeoutStrategy Create(string policyName);
}