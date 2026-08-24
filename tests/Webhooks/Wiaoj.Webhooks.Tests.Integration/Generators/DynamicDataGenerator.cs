using Bogus;
using System.Security.Cryptography;
using Wiaoj.Security;
using Wiaoj.Security.Testing;

namespace Wiaoj.Webhooks.Tests.Integration.Generators;

/// <summary>
/// Test data generator for integration tests.
/// </summary>
public static class DynamicDataGenerator {
    private static readonly FakeSecretProtector<WebhookSigningContext> Protector = new();

    private static readonly Faker<UserRegisteredWebhookEvent> UserEventFaker = new Faker<UserRegisteredWebhookEvent>()
        .CustomInstantiator(f => new UserRegisteredWebhookEvent(
            UserId: f.Random.Guid(),
            Email: f.Internet.Email(f.Name.FirstName(), f.Name.LastName(), "dynamic-test.io"),
            Username: f.Internet.UserName(),
            FullName: f.Name.FullName(),
            CountryCode: f.Address.CountryCode(),
            Tier: f.PickRandom("Starter", "Professional", "Enterprise", "Custom"),
            RegisteredAt: f.Date.RecentOffset(days: 1)
        ));

    /// <summary>
    /// Generates a randomized user onboarding webhook event.
    /// </summary>
    /// <returns>A new <see cref="UserRegisteredWebhookEvent"/> instance.</returns>
    public static UserRegisteredWebhookEvent CreateDynamicUserEvent() {
        return UserEventFaker.Generate();
    }

    /// <summary>
    /// Generates a list of randomized user onboarding webhook events.
    /// </summary>
    /// <param name="count">The number of events to generate.</param>
    /// <returns>A collection of <see cref="UserRegisteredWebhookEvent"/> instances.</returns>
    public static List<UserRegisteredWebhookEvent> CreateDynamicUserEvents(int count) {
        return UserEventFaker.Generate(count);
    }

    /// <summary>
    /// Generates a cryptographically secure, random 32-byte secret key string.
    /// </summary>
    /// <returns>A secret key string prefixed with <c>whsec_</c>.</returns>
    public static string GenerateRandomSecretKey() {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return $"whsec_{Convert.ToHexStringLower(randomBytes)}";
    }

    /// <summary>
    /// Generates a webhook endpoint with a dynamically generated endpoint ID, secret, and target URL matching the receiver route pattern.
    /// </summary>
    /// <param name="baseUrl">The base receiver endpoint URL.</param>
    /// <returns>A tuple containing the generated <see cref="WebhookEndpoint"/> and its unencrypted secret key.</returns>
    public static (WebhookEndpoint Endpoint, string RawSecret) CreateDynamicEndpoint(string baseUrl = "http://localhost/api/receiver") {
        string rawSecret = GenerateRandomSecretKey();
        EncryptedSecret<WebhookSigningContext> encryptedSecret = Protector.Protect(rawSecret);

        Faker faker = new();
        string slug = faker.Internet.DomainWord();
        WebhookEndpointId endpointId = new($"ep_{slug}_{Guid.NewGuid():N}");

        Uri targetUrl = new($"{baseUrl.TrimEnd('/')}/{endpointId.Value}");
        WebhookEndpoint endpoint = new(endpointId, targetUrl, encryptedSecret);
        return (endpoint, rawSecret);
    }
}

/// <summary>
/// Domain event representing user registration.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="Username">The user's chosen handle.</param>
/// <param name="FullName">The full name of the user.</param>
/// <param name="CountryCode">The ISO country code.</param>
/// <param name="Tier">The subscription tier.</param>
/// <param name="RegisteredAt">The registration timestamp.</param>
public sealed record UserRegisteredWebhookEvent(
    Guid UserId,
    string Email,
    string Username,
    string FullName,
    string CountryCode,
    string Tier,
    DateTimeOffset RegisteredAt) : IWebhookEvent {

    /// <inheritdoc/>
    public static string EventName => "user.registered";
}