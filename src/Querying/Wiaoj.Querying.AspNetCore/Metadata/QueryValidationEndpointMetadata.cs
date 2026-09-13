using Wiaoj.Preconditions;

namespace Wiaoj.Querying.AspNetCore;

/// <summary>
/// Marks an endpoint as accepting a schema-validated query, and records which entity's schema applies.
/// </summary>
/// <remarks>
/// <para>
/// The schema itself is resolved from the container when the endpoint filter is built, so it is not available
/// at convention time — but the entity type is, and that is enough for anything inspecting the endpoint later
/// to resolve <c>QuerySchema&lt;TEntity&gt;</c> for itself.
/// </para>
/// <para>
/// Without this, an endpoint that validates queries is indistinguishable from one that does not: the filter
/// factory leaves no trace on the endpoint. Tooling that reads endpoint metadata — OpenAPI document
/// generation above all — would have to infer the behaviour from handler signatures and would get it wrong
/// the first time a signature changed.
/// </para>
/// </remarks>
public sealed class QueryValidationEndpointMetadata {
    /// <summary>Gets the entity type whose <c>QuerySchema&lt;T&gt;</c> governs this endpoint.</summary>
    public Type EntityType { get; }

    /// <summary>
    /// Gets the schema class the endpoint selected, or <see langword="null"/> when it resolves the entity's only schema.
    /// </summary>
    /// <remarks>
    /// Everything that reads the endpoint's schema — validation, <c>Query&lt;T&gt;</c> binding, document generation —
    /// resolves this type when it is set, so all three apply the same contract.
    /// </remarks>
    public Type? SchemaType { get; }

    /// <summary>Initializes a new instance of the <see cref="QueryValidationEndpointMetadata"/> class.</summary>
    /// <param name="entityType">The entity type whose schema governs the endpoint.</param>
    public QueryValidationEndpointMetadata(Type entityType) : this(entityType, schemaType: null) { }

    /// <summary>Initializes a new instance for an endpoint that selected a schema class.</summary>
    /// <param name="entityType">The entity type whose schema governs the endpoint.</param>
    /// <param name="schemaType">The schema class, or <see langword="null"/> for the entity's only schema.</param>
    public QueryValidationEndpointMetadata(Type entityType, Type? schemaType) {
        Preca.ThrowIfNull(entityType);
        this.EntityType = entityType;
        this.SchemaType = schemaType;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return this.SchemaType is null
            ? $"QueryValidation({this.EntityType.Name})"
            : $"QueryValidation({this.EntityType.Name}, {this.SchemaType.Name})";
    }
}
