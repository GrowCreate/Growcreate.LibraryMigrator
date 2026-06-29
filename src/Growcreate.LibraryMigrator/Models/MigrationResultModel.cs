namespace Growcreate.LibraryMigrator.Models;

public class MigrationResultModel
{
    public bool Success { get; init; }
    public int ElementsCreated { get; init; }
    public List<Guid> LibraryFolderKeys { get; init; } = [];
    public List<string> Errors { get; init; } = [];

    public static MigrationResultModel Fail(string reason) => new()
    {
        Success = false,
        Errors = [reason],
    };
}
