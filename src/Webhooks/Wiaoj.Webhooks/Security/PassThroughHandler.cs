namespace Wiaoj.Webhooks.Security;

/// <summary>Takes the place of the proxied destination check when there is no proxy to check for.</summary>
internal sealed class PassThroughHandler : DelegatingHandler;
