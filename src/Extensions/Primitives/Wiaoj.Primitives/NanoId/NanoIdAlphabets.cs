namespace Wiaoj.Primitives;

public readonly partial record struct NanoId {
    /// <summary>
    /// Provides predefined alphabet sets for various NanoId generation scenarios.
    /// All predefined sets are strict subsets of the standard URL-safe alphabet.
    /// </summary>
    public static class Alphabets {
        /// <summary>
        /// The standard URL-safe alphabet consisting of alphanumeric characters, hyphens, and underscores.
        /// <para>Characters: 0-9, a-z, A-Z, _, - (64 characters)</para>
        /// </summary>
        /// <remarks>
        /// This set provides the maximum entropy (6 bits per character). 
        /// Use this when collision resistance is the top priority and URL safety is required.
        /// </remarks>
        public const string UrlSafe = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-";

        /// <summary>
        /// A corporate-safe alphabet that excludes vowels (a, e, i, o, u) to prevent the accidental generation 
        /// of profanity or meaningful offensive words in multiple languages.
        /// </summary>
        /// <remarks>
        /// Recommended for public-facing identifiers (e.g., resource IDs in URLs) where brand reputation 
        /// and professional appearance are critical.
        /// </remarks>
        public const string NoVowels = "0123456789bcdfghjklmnpqrstvwxyzBCDFGHJKLMNPQRSTVWXYZ_-";

        /// <summary>
        /// An alphanumeric alphabet excluding symbols (_ and -).
        /// <para>Characters: 0-9, a-z, A-Z (62 characters - Base62)</para>
        /// </summary>
        /// <remarks>
        /// Ideal for environments where punctuation marks cause parsing issues or break word selection in UI.
        /// </remarks>
        public const string Alphanumeric = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        /// A human-friendly alphabet designed to avoid confusion by excluding visually similar characters 
        /// such as (0, O), (1, l, I) and symbols.
        /// </summary>
        /// <remarks>
        /// Ideal for identifiers that need to be manually typed, read over the phone, or displayed 
        /// in environments where font legibility might be an issue.
        /// </remarks>
        public const string Readable = "23456789abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";

        /// <summary>
        /// A strictly lowercase alphanumeric alphabet (Base36).
        /// <para>Characters: 0-9, a-z (36 characters)</para>
        /// </summary>
        /// <remarks>
        /// Useful for case-insensitive database lookups, DNS labels, or subdomain-friendly identifiers.
        /// </remarks>
        public const string Lowercase = "0123456789abcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// A strictly uppercase alphanumeric alphabet (Base36).
        /// <para>Characters: 0-9, A-Z (36 characters)</para>
        /// </summary>
        /// <remarks>
        /// Ideal for license keys, short codes, or printed voucher references.
        /// </remarks>
        public const string Uppercase = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        /// A Crockford Base32 alphabet designed for human reading and error resistance.
        /// Excludes ambiguous characters (I, L, O) and vowels (U) to avoid accidental profanity.
        /// </summary>
        /// <remarks>
        /// Recommended for 2FA recovery codes, voucher codes, and manual reference keys.
        /// </remarks>
        public const string CrockfordBase32 = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        /// <summary>
        /// A lowercase hexadecimal alphabet.
        /// <para>Characters: 0-9, a-f (16 characters - Base16)</para>
        /// </summary>
        /// <remarks>
        /// Best suited for trace identifiers, correlation IDs, or hash-compatible hexadecimal representations.
        /// </remarks>
        public const string Hexadecimal = "0123456789abcdef";

        /// <summary>
        /// A strictly numeric alphabet containing only digits.
        /// </summary>
        /// <remarks>
        /// Best suited for generating One-Time Passwords (OTP), PIN codes, or numeric tracking references.
        /// </remarks>
        public const string Numeric = "0123456789";
    }
}