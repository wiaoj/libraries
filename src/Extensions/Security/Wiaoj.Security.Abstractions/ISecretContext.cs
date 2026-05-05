namespace Wiaoj.Security;

/// <summary>
/// Phantom/marker interface for defining a secret's domain.
/// Implement this on a sealed class to create a compile-time label
/// that prevents secrets from different domains from being mixed up.
/// </summary>
/// <remarks>
/// The phantom type pattern ensures that an <c>EncryptedSecret&lt;WebhookContext&gt;</c>
/// can never be accidentally passed to an <c>ISecretProtector&lt;ApiKeyContext&gt;</c> —
/// the mismatch is caught at compile time, not at runtime.
/// </remarks>
/// <example>
/// <code>
/// // Define your domains as tiny, empty sealed classes:
/// public sealed class WebhookSigningContext  : ISecretContext { }
/// public sealed class PaymentGatewayContext  : ISecretContext { }
/// public sealed class OAuthClientSecretContext : ISecretContext { }
///
/// // Now these are completely different, incompatible types:
/// EncryptedSecret&lt;WebhookSigningContext&gt;   webhookSecret = ...;
/// EncryptedSecret&lt;PaymentGatewayContext&gt;   paymentSecret = ...;
///
/// // This will NOT compile — type safety enforced:
/// // protector.Unprotect(paymentSecret);  // ← ERROR
/// </code>
/// </example>
public interface ISecretContext;