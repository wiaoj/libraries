using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;
using System.Net;

namespace Wiaoj.WellKnown.Tests.Integration;

/// <summary>
/// The served file follows RFC 9116, configuration the RFC does not allow fails at startup, and Expires is a fixed date
/// that is refused once past and warned about when too far or too close (#124).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "WellKnown")]
[Trait("Component", "SecurityTxt")]
public sealed class SecurityTxtTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InSixMonths = Now.AddMonths(6);

    private static void Minimal(SecurityTxtOptions file) {
        file.Contact.Add("mailto:security@example.com");
        file.Expires = InSixMonths;
    }

    private static Action<IServiceCollection> Services(Action<SecurityTxtOptions> configure, FakeTimeProvider? clock = null, LogCapture? logs = null) {
        return services => {
            services.AddSingleton<TimeProvider>(clock ?? new FakeTimeProvider(Now));
            if(logs is not null) {
                services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning).AddProvider(logs));
            }

            services.AddSecurityTxt(configure);
        };
    }

    private static async Task<(HttpResponseMessage Response, string Body)> FetchAsync(Action<IServiceCollection> services, string path = "/.well-known/security.txt") {
        await using TestApp app = await TestApp.StartAsync(services, a => a.MapSecurityTxt());
        HttpResponseMessage response = await app.Client.GetAsync(path, Ct);
        return (response, await response.Content.ReadAsStringAsync(Ct));
    }

    private static OptionsValidationException Refused(Action<SecurityTxtOptions> configure, FakeTimeProvider? clock = null) {
        return Assert.Throws<OptionsValidationException>(() => TestApp.Build(Services(configure, clock), a => a.MapSecurityTxt()));
    }

    internal sealed class LogCapture : ILoggerProvider {
        public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void Dispose() { }

        public string[] Warnings(string category = "Wiaoj.WellKnown.SecurityTxt") =>
            [.. this.Entries.Where(e => e.Category == category && e.Level == LogLevel.Warning).Select(e => e.Message)];

        private sealed class Logger(LogCapture capture, string category) : ILogger {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
                capture.Entries.Enqueue((category, logLevel, formatter(state, exception)));
            }
        }
    }

    public sealed class TheFile {
        [Fact]
        public async Task Should_Serve_Contact_And_Expires_As_Plain_Utf8_Text() {
            (HttpResponseMessage response, string body) = await FetchAsync(Services(Minimal));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
            Assert.Equal("public, max-age=86400", response.Headers.CacheControl?.ToString());
            Assert.Equal("Contact: mailto:security@example.com\nExpires: 2027-03-15T12:00:00Z\n", body);
        }

        [Fact]
        public async Task Should_Serve_Every_Rfc_9116_Field() {
            (_, string body) = await FetchAsync(Services(file => {
                file.Contact.Add("mailto:security@example.com");
                file.Contact.Add("tel:+1-201-555-0123");
                file.Contact.Add("https://example.com/security/report");
                file.Expires = InSixMonths;
                file.Encryption.Add("https://example.com/pgp-key.txt");
                file.Encryption.Add("openpgp4fpr:5f2de5521c63a801ab59ccb603d49de44b29100f");
                file.Acknowledgments.Add("https://example.com/hall-of-fame.html");
                file.PreferredLanguages.AddRange(["en", "tr", "pt-BR"]);
                file.Canonical.Add("https://example.com/.well-known/security.txt");
                file.Policy.Add("https://example.com/disclosure-policy.html");
                file.Hiring.Add("https://example.com/jobs.html");
                file.AdditionalFields.Add(new("CSAF", "https://example.com/.well-known/csaf/provider-metadata.json"));
            }));

            Assert.Equal(
                "Contact: mailto:security@example.com\n" +
                "Contact: tel:+1-201-555-0123\n" +
                "Contact: https://example.com/security/report\n" +
                "Expires: 2027-03-15T12:00:00Z\n" +
                "Encryption: https://example.com/pgp-key.txt\n" +
                "Encryption: openpgp4fpr:5f2de5521c63a801ab59ccb603d49de44b29100f\n" +
                "Acknowledgments: https://example.com/hall-of-fame.html\n" +
                "Preferred-Languages: en, tr, pt-BR\n" +
                "Canonical: https://example.com/.well-known/security.txt\n" +
                "Policy: https://example.com/disclosure-policy.html\n" +
                "Hiring: https://example.com/jobs.html\n" +
                "CSAF: https://example.com/.well-known/csaf/provider-metadata.json\n",
                body);
        }

        [Fact]
        public async Task Should_Write_Expires_In_Utc_Truncated_To_The_Second() {
            // 2027-01-10 01:30:45.999 +03:00 is 2027-01-09 22:30:45.999 UTC; truncating never writes a later date.
            (_, string body) = await FetchAsync(Services(file => {
                file.Contact.Add("mailto:security@example.com");
                file.Expires = new DateTimeOffset(2027, 1, 10, 1, 30, 45, 999, TimeSpan.FromHours(3));
            }));

            Assert.Contains("Expires: 2027-01-09T22:30:45Z\n", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_End_Every_Line_With_Lf_Only() {
            (_, string body) = await FetchAsync(Services(Minimal));

            Assert.DoesNotContain('\r', body);
            Assert.EndsWith("\n", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Send_No_Cache_When_The_Cache_Duration_Is_Zero() {
            (HttpResponseMessage response, _) = await FetchAsync(Services(file => {
                Minimal(file);
                file.CacheDuration = TimeSpan.Zero;
            }));

            Assert.True(response.Headers.CacheControl?.NoCache);
        }

        [Fact]
        public async Task Should_Bind_From_Configuration() {
            (_, string body) = await FetchAsync(services => {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
                IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                    ["SecurityTxt:Contact:0"] = "mailto:security@example.com",
                    ["SecurityTxt:Contact:1"] = "https://example.com/report",
                    ["SecurityTxt:Expires"] = "2027-06-30T00:00:00Z",
                    ["SecurityTxt:PreferredLanguages:0"] = "en",
                    ["SecurityTxt:Policy:0"] = "https://example.com/policy"
                }).Build();

                services.AddSecurityTxt();
                services.Configure<SecurityTxtOptions>(configuration.GetSection("SecurityTxt"));
            });

            Assert.Equal(
                "Contact: mailto:security@example.com\nContact: https://example.com/report\nExpires: 2027-06-30T00:00:00Z\n" +
                "Preferred-Languages: en\nPolicy: https://example.com/policy\n",
                body);
        }
    }

    public sealed class TheLegacyLocation {
        [Fact]
        public async Task Should_Redirect_Permanently_To_The_Well_Known_Path() {
            (HttpResponseMessage response, _) = await FetchAsync(Services(Minimal), "/security.txt");

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal("/.well-known/security.txt", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task Should_Not_Exist_When_The_Redirect_Is_Turned_Off() {
            (HttpResponseMessage response, _) = await FetchAsync(Services(file => {
                Minimal(file);
                file.RedirectLegacyPath = false;
            }), "/security.txt");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public sealed class Validation {
        [Fact]
        public void Should_Require_A_Contact_And_An_Expires_Reporting_Both_At_Once() {
            OptionsValidationException error = Refused(_ => { });

            Assert.Contains(error.Failures, f => f.Contains("no Contact", StringComparison.Ordinal));
            Assert.Contains(error.Failures, f => f.Contains("no Expires", StringComparison.Ordinal));
        }

        [Fact]
        public void Should_Refuse_An_Expires_Already_In_The_Past() {
            OptionsValidationException error = Refused(file => {
                file.Contact.Add("mailto:security@example.com");
                file.Expires = Now.AddSeconds(-1);
            });

            Assert.Contains("is in the past", Assert.Single(error.Failures), StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Refuse_An_Expires_Equal_To_Now() {
            Refused(file => {
                file.Contact.Add("mailto:security@example.com");
                file.Expires = Now;
            });
        }

        [Theory]
        [InlineData("http://example.com/report", "begin with https://")]
        [InlineData("security@example.com", "mailto:security@example.com")]
        [InlineData("not a uri", "not an absolute URI")]
        [InlineData("", "empty Contact")]
        [InlineData("mailto:security@example.com\nContact: mailto:attacker@evil.test", "line break")]
        public void Should_Refuse_A_Contact_That_Is_Not_A_Valid_Uri(string contact, string message) {
            OptionsValidationException error = Refused(file => {
                file.Contact.Add(contact);
                file.Expires = InSixMonths;
            });

            Assert.Contains(message, Assert.Single(error.Failures), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("tel:+1-201-555-0123")]
        [InlineData("mailto:security@example.com")]
        [InlineData("https://example.com/report")]
        [InlineData("HTTPS://example.com/report")]
        public void Should_Accept_Mailto_Tel_And_Https_Contacts(string contact) {
            using WebApplication app = TestApp.Build(Services(file => {
                file.Contact.Add(contact);
                file.Expires = InSixMonths;
            }), a => a.MapSecurityTxt());
        }

        [Theory]
        [InlineData("Encryption")]
        [InlineData("Acknowledgments")]
        [InlineData("Canonical")]
        [InlineData("Policy")]
        [InlineData("Hiring")]
        public void Should_Refuse_An_Http_Uri_In_Every_Uri_Field(string field) {
            OptionsValidationException error = Refused(file => {
                Minimal(file);
                (field switch {
                    "Encryption" => file.Encryption,
                    "Acknowledgments" => file.Acknowledgments,
                    "Canonical" => file.Canonical,
                    "Policy" => file.Policy,
                    _ => file.Hiring
                }).Add("http://example.com/x");
            });

            Assert.Contains($"{field} 'http://example.com/x' uses http", Assert.Single(error.Failures), StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Refuse_A_Key_In_The_Encryption_Field() {
            OptionsValidationException error = Refused(file => {
                Minimal(file);
                file.Encryption.Add("-----BEGIN PGP PUBLIC KEY BLOCK-----");
            });

            Assert.Contains("contains a key", Assert.Single(error.Failures), StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Accept_A_Dns_Encryption_Uri() {
            using WebApplication app = TestApp.Build(Services(file => {
                Minimal(file);
                file.Encryption.Add("dns:5d2d37ab76d47d36._openpgpkey.example.com?type=OPENPGPKEY");
            }), a => a.MapSecurityTxt());
        }

        [Theory]
        [InlineData("en,tr")]
        [InlineData("en_US")]
        [InlineData("1en")]
        [InlineData("en-toolongsubtag")]
        [InlineData("")]
        public void Should_Refuse_A_Value_That_Is_Not_A_Language_Tag(string language) {
            OptionsValidationException error = Refused(file => {
                Minimal(file);
                file.PreferredLanguages.Add(language);
            });

            Assert.Contains("is not a language tag", Assert.Single(error.Failures), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("contact", "https://example.com", "defined by RFC 9116")]
        [InlineData("Preferred-Languages", "en", "defined by RFC 9116")]
        [InlineData("Bad:Name", "x", "is not a field name")]
        [InlineData("Bad Name", "x", "is not a field name")]
        [InlineData("X-Custom", "", "has no value")]
        [InlineData("X-Custom", "value\r\nContact: mailto:attacker@evil.test", "line break")]
        public void Should_Refuse_An_Invalid_Additional_Field(string name, string value, string message) {
            OptionsValidationException error = Refused(file => {
                Minimal(file);
                file.AdditionalFields.Add(new(name, value));
            });

            Assert.Contains(message, Assert.Single(error.Failures), StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Refuse_A_Negative_Cache_Duration() {
            Refused(file => {
                Minimal(file);
                file.CacheDuration = TimeSpan.FromSeconds(-1);
            });
        }

        [Fact]
        public void Should_Refuse_A_File_Researchers_May_Not_Parse() {
            // RFC 9116 §5.4: more than 1,000 lines, a field longer than 2,048 characters, or more than 32 KB.
            Assert.Contains("1,000 lines", Assert.Single(Refused(file => {
                file.Expires = InSixMonths;
                file.Contact.AddRange(Enumerable.Range(0, 1000).Select(i => $"tel:+{i}"));
            }).Failures), StringComparison.Ordinal);

            Assert.Contains("2,048 characters", Assert.Single(Refused(file => {
                Minimal(file);
                file.Policy.Add("https://example.com/" + new string('a', 2048));
            }).Failures), StringComparison.Ordinal);

            Assert.Contains("32 KB", Assert.Single(Refused(file => {
                Minimal(file);
                file.Policy.AddRange(Enumerable.Range(0, 20).Select(i => $"https://example.com/{i}/" + new string('a', 2000)));
            }).Failures), StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Accept_A_File_At_The_Limits() {
            using WebApplication app = TestApp.Build(Services(file => {
                file.Expires = InSixMonths;
                file.Contact.AddRange(Enumerable.Range(0, 999).Select(i => $"tel:+{i}"));
            }), a => a.MapSecurityTxt());
        }

        [Fact]
        public void Should_Refuse_To_Map_When_Not_Registered() {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => TestApp.Build(_ => { }, a => a.MapSecurityTxt()));

            Assert.Contains("AddSecurityTxt", error.Message, StringComparison.Ordinal);
        }
    }

    public sealed class Expiry {
        private static string[] MapAndCollectWarnings(DateTimeOffset expires) {
            LogCapture logs = new();
            using WebApplication app = TestApp.Build(Services(file => {
                file.Contact.Add("mailto:security@example.com");
                file.Expires = expires;
            }, logs: logs), a => a.MapSecurityTxt());

            return logs.Warnings();
        }

        [Fact]
        public void Should_Warn_When_Expires_Is_More_Than_A_Year_Ahead() {
            Assert.Contains("more than a year ahead", Assert.Single(MapAndCollectWarnings(Now.AddDays(366))), StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Warn_When_Expires_Is_Less_Than_30_Days_Away() {
            Assert.Contains("is 10 days away", Assert.Single(MapAndCollectWarnings(Now.AddDays(10))), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(30)]
        [InlineData(180)]
        [InlineData(365)]
        public void Should_Not_Warn_Within_The_Recommended_Window(int days) {
            Assert.Empty(MapAndCollectWarnings(Now.AddDays(days)));
        }

        [Fact]
        public async Task Should_Keep_Serving_And_Warn_Once_When_Expires_Passes_While_Running() {
            FakeTimeProvider clock = new(Now);
            LogCapture logs = new();
            await using TestApp app = await TestApp.StartAsync(Services(file => {
                file.Contact.Add("mailto:security@example.com");
                file.Expires = Now.AddDays(60);
            }, clock, logs), a => a.MapSecurityTxt());

            using HttpResponseMessage fresh = await app.Client.GetAsync("/.well-known/security.txt", Ct);
            Assert.Empty(logs.Warnings());

            clock.Advance(TimeSpan.FromDays(61));
            using HttpResponseMessage first = await app.Client.GetAsync("/.well-known/security.txt", Ct);
            using HttpResponseMessage second = await app.Client.GetAsync("/.well-known/security.txt", Ct);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Contains("has passed", Assert.Single(logs.Warnings()), StringComparison.Ordinal);
        }
    }
}
