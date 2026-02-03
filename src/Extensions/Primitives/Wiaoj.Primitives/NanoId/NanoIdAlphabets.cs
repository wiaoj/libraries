namespace Wiaoj.Primitives;
public readonly partial record struct NanoId {
    /// <summary>
    /// Provides predefined alphabet sets for various NanoId generation scenarios.
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
        /// A human-friendly alphabet designed to avoid confusion by excluding visually similar characters 
        /// such as (0, O), (1, l, I).
        /// </summary>
        /// <remarks>
        /// Ideal for identifiers that need to be manually typed, read over the phone, or displayed 
        /// in environments where font legibility might be an issue.
        /// </remarks>
        public const string Readable = "23456789abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";

        /// <summary>
        /// A strictly numeric alphabet containing only digits.
        /// </summary>
        /// <remarks>
        /// Best suited for generating One-Time Passwords (OTP), PIN codes, or numeric tracking references.
        /// </remarks>
        public const string Numeric = "0123456789";
    }
}