namespace Growcreate.LibraryMigrator.Models;

public class EligibleDocTypeModel
{
    public Guid Key { get; init; }
    public string Alias { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int InstanceCount { get; init; }
    public bool IsAlreadyElement { get; init; }
    public bool IsAlreadyAllowedInLibrary { get; init; }
}
