using System.Runtime.CompilerServices;
using Wiaoj.Querying;
using Wiaoj.Querying.Parsers;

[assembly: InternalsVisibleTo("Wiaoj.Querying.Tests.Unit")]

// The query language moved to Wiaoj.Querying.Abstractions so a contract assembly can carry a QueryRequest
// without dependency injection. Code compiled against an earlier Wiaoj.Querying still looks these types up
// here; forwarding keeps it running without a rebuild. Namespaces did not change, so source is unaffected.
[assembly: TypeForwardedTo(typeof(Q))]
[assembly: TypeForwardedTo(typeof(QueryOperator))]
[assembly: TypeForwardedTo(typeof(QueryRequest))]
[assembly: TypeForwardedTo(typeof(FilterConditionNode))]
[assembly: TypeForwardedTo(typeof(Sort))]
[assembly: TypeForwardedTo(typeof(SortNode))]
[assembly: TypeForwardedTo(typeof(SortDirection))]
[assembly: TypeForwardedTo(typeof(QuerySyntax))]
[assembly: TypeForwardedTo(typeof(QueryValidationError))]
[assembly: TypeForwardedTo(typeof(QueryValidationErrorCode))]
[assembly: TypeForwardedTo(typeof(QueryValidationResult))]
[assembly: TypeForwardedTo(typeof(BracketQueryParser))]
[assembly: TypeForwardedTo(typeof(BracketQueryPayloadParser))]
[assembly: TypeForwardedTo(typeof(IQueryPayloadParser))]
[assembly: TypeForwardedTo(typeof(JsonQueryParser))]
[assembly: TypeForwardedTo(typeof(JsonQueryPayloadParser))]
