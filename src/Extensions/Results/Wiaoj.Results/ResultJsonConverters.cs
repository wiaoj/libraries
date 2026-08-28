using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Results;

/// <summary>
/// JSON converter factory for the open generic <see cref="Result{TValue}"/> type.
/// </summary>
public sealed class ResultJsonConverterFactory : JsonConverterFactory {

    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) {
        if(!typeToConvert.IsGenericType) return false;
        return typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);
    }

    /// <summary>
    /// NOTE (honest AOT caveat): this uses <see cref="Type.MakeGenericType(Type[])"/> +
    /// <see cref="Activator.CreateInstance(Type)"/> to instantiate <see cref="ResultJsonConverter{TValue}"/>
    /// for an arbitrary closed <c>TValue</c>. That genuinely requires dynamic code generation for
    /// value-type <c>TValue</c>s under Native AOT / full trimming — it is NOT "safe" in the sense of
    /// "works identically to source-generated converters everywhere". If you need guaranteed AOT support,
    /// register closed <see cref="ResultJsonConverter{TValue}"/> instances explicitly per-type instead of
    /// relying on this factory. The suppression below silences the analyzer; it does not remove the risk.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Open-generic converter creation via MakeGenericType requires dynamic code for value-type TValue under full AOT; callers targeting strict AOT should register closed converters explicitly instead of relying on this factory.")]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
        Justification = "ResultJsonConverter<TValue> has no members that would be trimmed away independently of TValue itself.")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
        Type valueType = typeToConvert.GetGenericArguments()[0];
        Type converterType = typeof(ResultJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// JSON converter for <see cref="Result{TValue}"/>.
/// Reads/writes are allocation-free on the property-name fast path via UTF-8 literal matching,
/// with a case-insensitive fallback for interop with payloads that don't match the exact casing.
/// </summary>
[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Serialization of generic TValue delegates to options-configured converters.")]
[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Serialization of generic TValue delegates to options-configured converters.")]
public sealed class ResultJsonConverter<TValue> : JsonConverter<Result<TValue>> {

    // Canonical member names (PascalCase, matching the actual C# members). The wire name actually
    // expected on the JSON is derived from these via options.PropertyNamingPolicy in both Read and
    // Write, so the two can never disagree regardless of which policy (if any) is configured.
    private const string IsSuccessMemberName = "IsSuccess";
    private const string ValueMemberName = "Value";
    private const string ErrorsMemberName = "Errors";

    /// <inheritdoc/>
    public override Result<TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token.");

        string wireIsSuccess = GetPropertyName(IsSuccessMemberName, options);
        string wireValue = GetPropertyName(ValueMemberName, options);
        string wireErrors = GetPropertyName(ErrorsMemberName, options);
        StringComparison comparison = options.PropertyNameCaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        bool isSuccess = false;
        bool isSuccessSet = false;
        bool valueFound = false;
        TValue? value = default;
        List<Error>? errors = null;

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndObject)
                break;

            if(reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName token.");

            // Fast path: exact wire-name match against the UTF-8 bytes with zero allocation.
            // This covers the overwhelming common case (default or camelCase policy, case-sensitive match).
            if(reader.ValueTextEquals(wireIsSuccess)) {
                reader.Read();
                isSuccess = reader.GetBoolean();
                isSuccessSet = true;
                continue;
            }
            if(reader.ValueTextEquals(wireValue)) {
                reader.Read();
                value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                valueFound = true;
                continue;
            }
            if(reader.ValueTextEquals(wireErrors)) {
                reader.Read();
                errors = JsonSerializer.Deserialize<List<Error>>(ref reader, options);
                continue;
            }

            // Slow path: only reached when the fast byte-exact match missed — e.g. PropertyNameCaseInsensitive
            // is set and the payload's casing differs from the computed wire name.
            string? propertyName = reader.GetString();
            reader.Read();

            if(string.Equals(propertyName, wireIsSuccess, comparison)) {
                isSuccess = reader.GetBoolean();
                isSuccessSet = true;
            }
            else if(string.Equals(propertyName, wireValue, comparison)) {
                value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                valueFound = true;
            }
            else if(string.Equals(propertyName, wireErrors, comparison)) {
                errors = JsonSerializer.Deserialize<List<Error>>(ref reader, options);
            }
            else {
                reader.Skip();
            }
        }

        if(!isSuccessSet)
            throw new JsonException("JSON payload is missing required 'isSuccess' property.");

        if(isSuccess) {
            if(typeof(TValue) == typeof(Success))
                return (Result<TValue>)(object)Result.Success();

            // A success payload that never carried a 'value' property is ambiguous data, not a valid
            // default. Silently returning default(TValue) here is exactly the "fabricated metadata"
            // failure mode — surface it instead of pretending it deserialized correctly.
            if(!valueFound)
                throw new JsonException($"JSON payload declares isSuccess=true for Result<{typeof(TValue).Name}> but is missing the required 'value' property.");

            return Result.Success(value!);
        }

        if(errors is null || errors.Count == 0)
            return Result.Failure<TValue>(Error.Unexpected("Json.Deserialization", "Failure result contained no errors."));

        return errors;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Result<TValue> value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WriteBoolean(GetPropertyName(IsSuccessMemberName, options), value.IsSuccess);

        if(value.IsSuccess) {
            if(typeof(TValue) != typeof(Success)) {
                writer.WritePropertyName(GetPropertyName(ValueMemberName, options));
                JsonSerializer.Serialize(writer, value.Value, options);
            }
        }
        else {
            writer.WritePropertyName(GetPropertyName(ErrorsMemberName, options));
            JsonSerializer.Serialize(writer, value.Errors, options);
        }

        writer.WriteEndObject();
    }

    private static string GetPropertyName(string canonicalName, JsonSerializerOptions options)
        => options.PropertyNamingPolicy?.ConvertName(canonicalName) ?? canonicalName;
}

/// <summary>
/// JSON converter for <see cref="Error"/>.
/// </summary>
[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Metadata dictionary serialization delegates to options-configured converters.")]
[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Metadata dictionary serialization delegates to options-configured converters.")]
public sealed class ErrorJsonConverter : JsonConverter<Error> {

    private const string CodeMemberName = "Code";
    private const string DescriptionMemberName = "Description";
    private const string TypeMemberName = "Type";
    private const string MetadataMemberName = "Metadata";

    /// <inheritdoc/>
    public override Error Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token.");

        string wireCode = GetPropertyName(CodeMemberName, options);
        string wireDescription = GetPropertyName(DescriptionMemberName, options);
        string wireType = GetPropertyName(TypeMemberName, options);
        string wireMetadata = GetPropertyName(MetadataMemberName, options);
        StringComparison comparison = options.PropertyNameCaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        string? code = null;
        string description = string.Empty;
        ErrorType type = ErrorType.Failure;
        Dictionary<string, object?>? metadata = null;

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndObject)
                break;

            if(reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName token.");

            if(reader.ValueTextEquals(wireCode)) {
                reader.Read();
                code = reader.GetString();
                continue;
            }
            if(reader.ValueTextEquals(wireDescription)) {
                reader.Read();
                description = reader.GetString() ?? description;
                continue;
            }
            if(reader.ValueTextEquals(wireType)) {
                reader.Read();
                string? typeName = reader.GetString();
                if(!string.IsNullOrWhiteSpace(typeName))
                    type = new ErrorType(typeName);
                continue;
            }
            if(reader.ValueTextEquals(wireMetadata)) {
                reader.Read();
                metadata = JsonSerializer.Deserialize<Dictionary<string, object?>>(ref reader, options);
                continue;
            }

            string? propertyName = reader.GetString();
            reader.Read();

            if(string.Equals(propertyName, wireCode, comparison)) {
                code = reader.GetString();
            }
            else if(string.Equals(propertyName, wireDescription, comparison)) {
                description = reader.GetString() ?? description;
            }
            else if(string.Equals(propertyName, wireType, comparison)) {
                string? typeName = reader.GetString();
                if(!string.IsNullOrWhiteSpace(typeName))
                    type = new ErrorType(typeName);
            }
            else if(string.Equals(propertyName, wireMetadata, comparison)) {
                metadata = JsonSerializer.Deserialize<Dictionary<string, object?>>(ref reader, options);
            }
            else {
                reader.Skip();
            }
        }

        // 'code' is the identity of an Error — silently substituting a fabricated default here
        // (as the previous implementation did with "General.Failure") hides malformed payloads
        // instead of surfacing them.
        if(string.IsNullOrWhiteSpace(code))
            throw new JsonException("JSON payload is missing the required 'code' property for Error.");

        return new Error(code, description, type, metadata);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Error value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WriteString(GetPropertyName(CodeMemberName, options), value.Code);
        writer.WriteString(GetPropertyName(DescriptionMemberName, options), value.Description);
        writer.WriteString(GetPropertyName(TypeMemberName, options), value.Type.Name);

        if(value.Metadata is not null && value.Metadata.Count > 0) {
            writer.WritePropertyName(GetPropertyName(MetadataMemberName, options));
            JsonSerializer.Serialize(writer, value.Metadata, options);
        }

        writer.WriteEndObject();
    }

    private static string GetPropertyName(string canonicalName, JsonSerializerOptions options)
        => options.PropertyNamingPolicy?.ConvertName(canonicalName) ?? canonicalName;
}

/// <summary>
/// JSON converter for <see cref="ErrorType"/>.
/// </summary>
public sealed class ErrorTypeJsonConverter : JsonConverter<ErrorType> {

    /// <inheritdoc/>
    public override ErrorType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected a string token for ErrorType.");

        string? name = reader.GetString();
        return string.IsNullOrWhiteSpace(name) ? ErrorType.Failure : new ErrorType(name);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ErrorType value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Name);
    }
}