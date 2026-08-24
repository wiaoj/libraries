using Microsoft.Extensions.Options;

namespace Wiaoj.Webhooks.Security;

/// <summary>
/// Wires <see cref="WebhookSecurityOptions.Validate"/> into the <see cref="IOptions{TOptions}"/> validation
/// pipeline so misconfiguration (whether set via the builder, <c>appsettings.json</c>, or
/// <see cref="IOptionsMonitor{TOptions}"/>) fails fast at host startup instead of on the first webhook delivery.
/// </summary>
internal sealed class WebhookSecurityOptionsValidator : IValidateOptions<WebhookSecurityOptions> {
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, WebhookSecurityOptions options) {
        try {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch(ArgumentOutOfRangeException ex) {
            return ValidateOptionsResult.Fail($"WebhookSecurityOptions is invalid: {ex.Message}");
        }
    }
}