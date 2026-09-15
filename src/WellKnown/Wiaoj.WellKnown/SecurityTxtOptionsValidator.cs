using Microsoft.Extensions.Options;
using System.Text;
using static Wiaoj.WellKnown.WellKnownValidation;

namespace Wiaoj.WellKnown;

/// <summary>
/// Rejects <see cref="SecurityTxtOptions"/> that would publish a file RFC 9116 does not allow, or one already expired.
/// </summary>
/// <remarks>Every rule is checked and every failure reported in one result.</remarks>
internal sealed class SecurityTxtOptionsValidator(TimeProvider timeProvider) : IValidateOptions<SecurityTxtOptions> {
    private const string Label = "security.txt";

    public ValidateOptionsResult Validate(string? name, SecurityTxtOptions options) {
        if(!string.IsNullOrEmpty(name)) {
            return ValidateOptionsResult.Skip;
        }

        List<string> failures = [];

        // §2.5.3: Contact MUST always be present.
        if(options.Contact.Count == 0) {
            failures.Add($"{Label} has no Contact. RFC 9116 requires at least one: a mailto:, tel: or https:// URI.");
        }

        foreach(string contact in options.Contact) {
            RequireFieldUri(contact, "Contact", failures);
        }

        // §2.5.5: Expires MUST always be present; a file already expired is refused rather than published stale (§5.3).
        if(options.Expires is not { } expires) {
            failures.Add($"{Label} has no Expires. RFC 9116 requires it; configure a fixed date less than a year ahead.");
        }
        else if(expires <= timeProvider.GetUtcNow()) {
            failures.Add(
                $"{Label} Expires '{SecurityTxtDocument.FormatExpires(expires)}' is in the past, so researchers would treat the file " +
                "as stale. Review the file's contents and set a new date.");
        }

        foreach(string encryption in options.Encryption) {
            // §2.5.4: keys MUST NOT appear in this field.
            if(encryption.Contains("-----BEGIN", StringComparison.Ordinal)) {
                failures.Add($"{Label} Encryption contains a key. RFC 9116 forbids keys in the file; publish the key and give its URI.");
                continue;
            }

            RequireFieldUri(encryption, "Encryption", failures);
        }

        foreach(string acknowledgments in options.Acknowledgments) {
            RequireFieldUri(acknowledgments, "Acknowledgments", failures);
        }

        foreach(string canonical in options.Canonical) {
            RequireFieldUri(canonical, "Canonical", failures);
        }

        foreach(string policy in options.Policy) {
            RequireFieldUri(policy, "Policy", failures);
        }

        foreach(string hiring in options.Hiring) {
            RequireFieldUri(hiring, "Hiring", failures);
        }

        foreach(string language in options.PreferredLanguages) {
            if(!IsLanguageTag(language)) {
                failures.Add($"{Label} Preferred-Languages value '{Printable(language)}' is not a language tag (RFC 5646), such as 'en' or 'pt-BR'.");
            }
        }

        foreach(KeyValuePair<string, string> field in options.AdditionalFields) {
            RequireAdditionalField(field.Key, field.Value, failures);
        }

        RequireCacheDuration(options.CacheDuration, Label, failures);

        // §5.4: researchers may not parse a larger file, so a file within the limits is the one that gets read.
        if(failures.Count == 0) {
            RequireParseableSize(SecurityTxtDocument.Render(options), failures);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// A field value that is a URI (RFC 3986), and a web URI only over https — every URI field in RFC 9116 §2.5 says "If
    /// this field indicates a web URI, then it MUST begin with 'https://'".
    /// </summary>
    private static void RequireFieldUri(string? value, string field, List<string> failures) {
        if(string.IsNullOrWhiteSpace(value)) {
            failures.Add($"{Label} has an empty {field}; a field MUST always consist of a name and a value.");
            return;
        }

        if(ContainsLineBreak(value)) {
            failures.Add($"{Label} {field} '{Printable(value)}' contains a line break, which would start another field.");
            return;
        }

        if(!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || value.Contains(' ')) {
            string hint = field == "Contact" && value.Contains('@') && !value.Contains(':')
                ? " An email address is written as a mailto: URI, for example 'mailto:" + value + "'."
                : "";
            failures.Add($"{Label} {field} '{value}' is not an absolute URI.{hint}");
            return;
        }

        if(uri.Scheme == Uri.UriSchemeHttp) {
            failures.Add($"{Label} {field} '{value}' uses http; RFC 9116 requires a web URI to begin with https://.");
        }
        else if(uri.Scheme == Uri.UriSchemeHttps && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
            failures.Add($"{Label} {field} '{value}' must begin with https://.");
        }
    }

    private static void RequireAdditionalField(string name, string value, List<string> failures) {
        if(string.IsNullOrWhiteSpace(name) || !IsFieldName(name)) {
            failures.Add(
                $"{Label} additional field name '{Printable(name ?? "")}' is not a field name: printable US-ASCII characters " +
                "other than ':' and space (RFC 5322 §3.6.8).");
            return;
        }

        if(SecurityTxtDocument.StandardFieldNames.Contains(name)) {
            failures.Add(
                $"{Label} additional field '{name}' is defined by RFC 9116; set it through its own option, where it is validated, " +
                "instead of publishing it unchecked.");
            return;
        }

        if(string.IsNullOrWhiteSpace(value)) {
            failures.Add($"{Label} additional field '{name}' has no value; a field MUST always consist of a name and a value.");
        }
        else if(ContainsLineBreak(value)) {
            failures.Add($"{Label} additional field '{name}' contains a line break, which would start another field.");
        }
    }

    private static void RequireParseableSize(string file, List<string> failures) {
        if(Encoding.UTF8.GetByteCount(file) > SecurityTxtDocument.MaxBytes) {
            failures.Add($"{Label} is larger than 32 KB, which RFC 9116 §5.4 lets researchers refuse to parse.");
        }

        string[] lines = file.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if(lines.Length > SecurityTxtDocument.MaxLines) {
            failures.Add($"{Label} has more than 1,000 lines, which RFC 9116 §5.4 lets researchers refuse to parse.");
        }

        foreach(string line in lines) {
            if(line.Length > SecurityTxtDocument.MaxFieldLength) {
                failures.Add($"{Label} field '{line[..Math.Min(40, line.Length)]}…' is longer than 2,048 characters, which RFC 9116 §5.4 lets researchers refuse to parse.");
            }
        }
    }

    private static bool ContainsLineBreak(string value) => value.AsSpan().IndexOfAny('\r', '\n') >= 0;

    private static string Printable(string value) => value.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>RFC 5322 §3.6.8: <c>ftext = %d33-57 / %d59-126</c>.</summary>
    private static bool IsFieldName(string name) {
        foreach(char c in name) {
            if(c is < '\x21' or ':' or > '\x7E') {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The RFC 5646 shape: subtags of 1–8 ASCII letters or digits separated by hyphens, the first all letters. Registry
    /// membership is not checked.
    /// </summary>
    private static bool IsLanguageTag(string tag) {
        if(string.IsNullOrEmpty(tag)) {
            return false;
        }

        string[] subtags = tag.Split('-');
        for(int i = 0; i < subtags.Length; i++) {
            string subtag = subtags[i];
            if(subtag.Length is 0 or > 8) {
                return false;
            }

            foreach(char c in subtag) {
                bool letter = c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
                bool digit = c is >= '0' and <= '9';
                if(!(letter || (digit && i > 0))) {
                    return false;
                }
            }
        }

        return true;
    }
}
