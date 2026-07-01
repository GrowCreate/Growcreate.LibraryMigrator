namespace Growcreate.LibraryMigrator.Models;

/// <summary>
/// A document type eligible for site-wide migration to the Umbraco Library:
/// a template-less type that has at least one instance in the content tree.
/// </summary>
public class EligibleTypeModel
{
    public Guid TypeKey { get; init; }
    public string TypeAlias { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public int DocumentCountSitewide { get; init; }
}
