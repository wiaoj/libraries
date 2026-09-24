using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wiaoj.Identifiers;
using Wiaoj.Identifiers.DependencyInjection;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Registers the <see cref="IdCodec"/> identifiers are written and read with.
/// </summary>
public static class IdentifiersServiceCollectionExtensions {
    /// <summary>
    /// Registers identifier support. Choose a codec on the returned builder; the codec is installed as
    /// <see cref="IdCodec.Current"/> when the host starts.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>A builder to choose the codec with.</returns>
    /// <example>
    /// <code>
    /// // Encrypted, with the key from configuration ("Identifiers:AesKey", base64)
    /// services.AddIdentifiers().UseAesCodec();
    /// services.Configure&lt;IdentifiersOptions&gt;(configuration.GetSection("Identifiers"));
    ///
    /// // Plain: readable, reveals creation time
    /// services.AddIdentifiers().UsePlainCodec();
    /// </code>
    /// </example>
    /// <remarks>
    /// Starting the host without choosing a codec fails, rather than writing identifiers with a codec nobody chose.
    /// Without a host, call <see cref="UseIdentifiers(IServiceProvider)"/> on the built provider.
    /// </remarks>
    public static IIdentifiersBuilder AddIdentifiers(this IServiceCollection services) {
        Preca.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, IdCodecInstaller>());
        return new IdentifiersBuilder(services);
    }

    /// <summary>
    /// Registers identifier support and chooses the codec inside <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Chooses the codec on the builder.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="configure"/> chose no codec.</exception>
    /// <remarks>
    /// Unlike <see cref="AddIdentifiers(IServiceCollection)"/>, whose configuration can continue after it returns, the
    /// configuration here is complete when <paramref name="configure"/> returns, so a missing codec fails on this line
    /// rather than when the host starts.
    /// </remarks>
    public static IServiceCollection AddIdentifiers(this IServiceCollection services, Action<IIdentifiersBuilder> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        configure(services.AddIdentifiers());

        if(!services.Any(static d => d.ServiceType == typeof(IdCodec))) {
            throw new InvalidOperationException(
                "AddIdentifiers(configure) chose no codec. Call UsePlainCodec(), UseAesCodec() or UseCodec(...) on the builder.");
        }

        return services;
    }

    /// <summary>
    /// Installs the registered <see cref="IdCodec"/> as <see cref="IdCodec.Current"/> — for an application that builds a
    /// service provider without a host.
    /// </summary>
    /// <param name="services">The built service provider.</param>
    /// <returns>The service provider, for chaining.</returns>
    /// <exception cref="InvalidOperationException">No codec was chosen, or a different codec is already installed.</exception>
    /// <exception cref="OptionsValidationException">The codec's options are invalid.</exception>
    public static IServiceProvider UseIdentifiers(this IServiceProvider services) {
        Preca.ThrowIfNull(services);

        IdCodecInstaller.Install(services);
        return services;
    }
}

/// <summary>Options for <see cref="IdentifiersBuilderExtensions.UseAesCodec(IIdentifiersBuilder)"/>.</summary>
public sealed class IdentifiersOptions {
    /// <summary>
    /// Gets or sets the AES codec key as base64: at least 16 bytes of random data, for example from
    /// <c>RandomNumberGenerator.GetBytes(32)</c>. Keep it in a secret store, not in source control.
    /// </summary>
    public string? AesKey { get; set; }

    /// <summary>Gets or sets the key version written into every identifier. Defaults to <c>'1'</c>.</summary>
    public char AesKeyVersion { get; set; } = '1';
}

internal sealed class IdentifiersOptionsValidator : IValidateOptions<IdentifiersOptions> {
    public ValidateOptionsResult Validate(string? name, IdentifiersOptions options) {
        List<string> failures = [];

        if(string.IsNullOrWhiteSpace(options.AesKey)) {
            failures.Add("Identifiers AesKey is not set. Configure a base64 key of at least 16 random bytes; there is no default key.");
        }
        else {
            byte[] key = new byte[(options.AesKey.Length * 3 / 4) + 3];
            if(!Convert.TryFromBase64String(options.AesKey, key, out int length)) {
                failures.Add("Identifiers AesKey is not valid base64.");
            }
            else if(length < AesIdCodec.MinimumKeyLength) {
                failures.Add($"Identifiers AesKey is {length} bytes; at least {AesIdCodec.MinimumKeyLength} random bytes are required.");
            }
        }

        if(!char.IsAsciiLetterOrDigit(options.AesKeyVersion)) {
            failures.Add($"Identifiers AesKeyVersion '{options.AesKeyVersion}' must be an ASCII letter or digit.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>Installs the registered codec as <see cref="IdCodec.Current"/> before any other hosted service starts.</summary>
internal sealed class IdCodecInstaller(IServiceProvider services) : IHostedLifecycleService {
    public static void Install(IServiceProvider services) {
        IdCodec codec = services.GetService<IdCodec>() ?? throw new InvalidOperationException(
            "AddIdentifiers() was called without choosing a codec. Call UsePlainCodec(), UseAesCodec() or UseCodec(...).");

        IdCodec.Install(codec);
    }

    public Task StartingAsync(CancellationToken cancellationToken) {
        Install(services);
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
