using System.Text.Json.Serialization;

namespace Growcreate.LibraryMigrator.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationState
{
    NotMigrated,
    Migrated,
    PartiallyMigrated,
}

public class MigrationStatusModel
{
    public bool HasMigration { get; init; }
    public MigrationState State { get; init; } = MigrationState.NotMigrated;
    public DateTime? MigratedAt { get; init; }
    public int ElementCount { get; init; }
    public int ErrorCount { get; init; }
    public List<string> Errors { get; init; } = [];
}
