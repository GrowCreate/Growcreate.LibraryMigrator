namespace Growcreate.LibraryMigrator.Models;

public class PreviewReportModel
{
    public string ContainerName { get; set; } = string.Empty;
    public Guid ContainerKey { get; set; }
    public bool Migratable { get; set; }
    public List<MigratableTypeReport> Types { get; set; } = [];
    public List<PickerPropertyUsage> AffectedPickers { get; set; } = [];
    public List<PickerPropertyUsage> AffectedBlockEditors { get; set; } = [];
    public List<PickerPropertyUsage> AffectedMediaPickers { get; set; } = [];
    public List<PickerPropertyUsage> AffectedMediaBlockEditors { get; set; } = [];
    public List<PickerPropertyUsage> AffectedMemberPickers { get; set; } = [];
    public List<PickerPropertyUsage> AffectedMemberBlockEditors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Blockers { get; set; } = [];
}

public class MigratableTypeReport
{
    public string TypeAlias { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public Guid TypeKey { get; set; }
    public bool IsAlreadyElement { get; set; }
    public bool IsAlreadyAllowedInLibrary { get; set; }
    public int DocumentCountSitewide { get; set; }
    public int DirectChildCount { get; set; }
}

public class PickerPropertyUsage
{
    public string PropertyAlias { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string OwnerDocTypeAlias { get; set; } = string.Empty;
    public string OwnerDocTypeName { get; set; } = string.Empty;
    public string PickerEditorAlias { get; set; } = string.Empty;
    public string DataTypeName { get; set; } = string.Empty;
}
