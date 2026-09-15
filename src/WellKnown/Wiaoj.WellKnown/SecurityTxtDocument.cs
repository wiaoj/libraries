using System.Globalization;
using System.Text;

namespace Wiaoj.WellKnown;

/// <summary>Writes <see cref="SecurityTxtOptions"/> as an RFC 9116 file.</summary>
internal static class SecurityTxtDocument {
    public const string WellKnownPath = "/.well-known/security.txt";
    public const string LegacyPath = "/security.txt";
    public const string ContentType = "text/plain; charset=utf-8";

    /// <summary>RFC 9116 §5.4: a researcher may not parse a file larger than 32 KB…</summary>
    public const int MaxBytes = 32 * 1024;

    /// <summary>…a field longer than 2,048 characters…</summary>
    public const int MaxFieldLength = 2048;

    /// <summary>…or more than 1,000 lines.</summary>
    public const int MaxLines = 1000;

    /// <summary>The field names RFC 9116 §2.5 defines; field names are case insensitive (§2).</summary>
    public static readonly IReadOnlySet<string> StandardFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "Acknowledgments", "Canonical", "Contact", "Encryption", "Expires", "Hiring", "Policy", "Preferred-Languages"
    };

    /// <summary>Returns the fields, one per line, each ending with LF (§2.2).</summary>
    /// <remarks>
    /// Contact and Expires come first, being the fields every file has; the optional fields follow in a fixed order, then
    /// the additional ones as configured. Values are written as they are: the validator has already refused line breaks.
    /// </remarks>
    public static string Render(SecurityTxtOptions options) {
        StringBuilder file = new();

        foreach(string contact in options.Contact) {
            Field(file, "Contact", contact);
        }

        if(options.Expires is { } expires) {
            Field(file, "Expires", FormatExpires(expires));
        }

        foreach(string encryption in options.Encryption) {
            Field(file, "Encryption", encryption);
        }

        foreach(string acknowledgments in options.Acknowledgments) {
            Field(file, "Acknowledgments", acknowledgments);
        }

        if(options.PreferredLanguages.Count > 0) {
            Field(file, "Preferred-Languages", string.Join(", ", options.PreferredLanguages));
        }

        foreach(string canonical in options.Canonical) {
            Field(file, "Canonical", canonical);
        }

        foreach(string policy in options.Policy) {
            Field(file, "Policy", policy);
        }

        foreach(string hiring in options.Hiring) {
            Field(file, "Hiring", hiring);
        }

        foreach(KeyValuePair<string, string> field in options.AdditionalFields) {
            Field(file, field.Key, field.Value);
        }

        return file.ToString();
    }

    /// <summary>An RFC 3339 <c>date-time</c> in UTC, to the second — truncated, so the written date is never later than the configured one.</summary>
    public static string FormatExpires(DateTimeOffset expires) {
        DateTimeOffset utc = expires.ToUniversalTime();
        utc = utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerSecond));
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static void Field(StringBuilder file, string name, string value) {
        file.Append(name).Append(": ").Append(value).Append('\n');
    }
}
