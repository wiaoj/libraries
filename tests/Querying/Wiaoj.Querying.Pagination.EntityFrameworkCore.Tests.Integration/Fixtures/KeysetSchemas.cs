using System.Globalization;

namespace Wiaoj.Querying.Pagination.EntityFrameworkCore.Tests.Integration.Fixtures;

/// <summary>Priority and file name page by cursor; file size sorts, but only on offset endpoints.</summary>
public class KeysetAssetSchema : QuerySchema<Asset, AssetSummary> {
    public KeysetAssetSchema() {
        Project(a => new AssetSummary(a.Id, a.FileName, a.FileSize));
        AllowFilter(a => a.FileName);
        AllowFilter(a => a.FileSize);
        AllowSort(a => a.FileSize);
        Property(a => a.FileName).AsCursor();
        Property(a => a.Priority).AsCursor().NotInResponse();
        RequireFilter(a => !a.IsDeleted);
        TieBreaker(a => a.Id);
    }
}

public sealed class DefaultSortedKeysetSchema : KeysetAssetSchema {
    public DefaultSortedKeysetSchema() {
        DefaultSort(a => a.Priority, SortDirection.Descending);
    }
}

public sealed class DefaultSortNotCursorSchema : KeysetAssetSchema {
    public DefaultSortNotCursorSchema() {
        DefaultSort(a => a.FileSize);
    }
}

public enum Kind { Image = 1, Video = 2, Audio = 3 }

public readonly record struct DocumentId(long Value) : IComparable<DocumentId> {
    public int CompareTo(DocumentId other) => this.Value.CompareTo(other.Value);
}

public sealed class Document {
    public DocumentId Id { get; set; }
    public Kind Kind { get; set; }
    public string? Title { get; set; }
}

public sealed record DocumentSummary(long Id, Kind Kind, string? Title);

public sealed class DocumentSchema : QuerySchema<Document, DocumentSummary> {
    public DocumentSchema() {
        Project(d => new DocumentSummary(d.Id.Value, d.Kind, d.Title));
        Property(d => d.Kind).AsCursor();
        Property(d => d.Title).AsCursor();
        TieBreaker(d => d.Id,
            id => id.Value.ToString(CultureInfo.InvariantCulture),
            text => new DocumentId(long.Parse(text, CultureInfo.InvariantCulture)));
    }
}

public sealed class DocumentWithoutTieBreakerCodecSchema : QuerySchema<Document, DocumentSummary> {
    public DocumentWithoutTieBreakerCodecSchema() {
        Project(d => new DocumentSummary(d.Id.Value, d.Kind, d.Title));
        Property(d => d.Kind).AsCursor();
        TieBreaker(d => d.Id);
    }
}
