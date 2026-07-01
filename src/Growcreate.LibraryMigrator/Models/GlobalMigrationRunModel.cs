namespace Growcreate.LibraryMigrator.Models;

public class GlobalMigrationRunModel
{
    public Guid RunId { get; init; }
    public Guid DocTypeKey { get; init; }
    public string DocTypeAlias { get; init; } = string.Empty;
    public string DocTypeName { get; init; } = string.Empty;
    public DateTime? MigratedAt { get; init; }
    public int ElementCount { get; init; }
    public int ErrorCount { get; init; }
    public MigrationState State { get; init; } = MigrationState.NotMigrated;
    public List<string> Errors { get; init; } = [];
}
