using System.Text;
using Wiaoj.Primitives;

namespace Wiaoj.Extensions;
public static class EncodingExtensions {
    extension(string text) {
        /// <summary>
        /// Encodes the string to a <see cref="Base64String"/> using UTF-8 encoding.
        /// </summary>
        public Base64String ToBase64() {
            return Base64String.FromUtf8(text);
        }

        /// <summary>
        /// Encodes the string to a <see cref="Base64String"/> using the specified encoding.
        /// </summary>
        public Base64String ToBase64(Encoding encoding) {
            return Base64String.From(text, encoding);
        }

        /// <summary>
        /// Encodes the string to a <see cref="HexString"/> using UTF-8 encoding.
        /// </summary>
        public HexString ToHex() {
            return HexString.FromUtf8(text);
        }

        /// <summary>
        /// Encodes the string to a <see cref="Base32String"/> using UTF-8 encoding.
        /// </summary>
        public Base32String ToBase32() {
            return Base32String.FromUtf8(text);
        }

        /// <summary>
        /// Wraps the string into a secure <see cref="Secret{Byte}"/> using UTF-8 encoding.
        /// </summary>
        public Secret<byte> ToSecret() {
            return Secret.From(text);
        }
    }

    extension(byte[] bytes) {
        /// <summary>
        /// Wraps the byte array into a secure <see cref="Secret{Byte}"/>.
        /// </summary>
        public Secret<byte> ToSecret() {
            return Secret.From(bytes);
        }

        /// <summary>
        /// Encodes the byte array to a <see cref="Base64String"/>.
        /// </summary>
        public Base64String ToBase64() {
            return Base64String.FromBytes(bytes);
        }

        /// <summary>
        /// Encodes the byte array to a <see cref="HexString"/>.
        /// </summary>
        public HexString ToHex() {
            return HexString.FromBytes(bytes);
        }
    }

    extension(ReadOnlySpan<byte> bytes) {
        /// <summary>
        /// Wraps the byte span into a secure <see cref="Secret{Byte}"/>.
        /// </summary>
        public Secret<byte> ToSecret() {
            return Secret.From(bytes);
        }
    }
}