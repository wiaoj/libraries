namespace Wiaoj.Webhooks.Tests.Unit.TestData;

internal static class WebhookTestConstants {
    public const string EndpointIdValue = "acme-1";
    public const string TargetUrlValue = "https://acme.com/webhooks/wiaoj";
    public const string SecretValue = "whsec_test_secret_value_secure_32bytes_long_key_12345";
    public const string EventTypeValue = "order.created";
    public const string PayloadJson = """{"orderId":"ORD-1","amount":42.50}""";
}