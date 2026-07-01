namespace Growcreate.LibraryMigrator.Models;

public class GlobalPreviewReportModel
{
    public Guid DocTypeKey { get; init; }
    public string DocTypeAlias { get; init; } = string.Empty;
    public string DocTypeName { get; init; } = string.Empty;
    public bool Migratable { get; init; }
    public int InstanceCount { get; init; }
    public int DistinctParentCount { get; init; }
    public bool IsAlreadyElement { get; init; }
    public bool IsAlreadyAllowedInLibrary { get; init; }
    public List<PickerPropertyUsage> AffectedPickers { get; init; } = [];
    public List<PickerPropertyUsage> AffectedBlockEditors { get; init; } = [];
    public List<PickerPropertyUsage> AffectedMediaPickers { get; init; } = [];
    public List<PickerPropertyUsage> AffectedMediaBlockEditors { get; init; } = [];
    public List<PickerPropertyUsage> AffectedMemberPickers { get; init; } = [];
    public List<PickerPropertyUsage> AffectedMemberBlockEditors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> Blockers { get; init; } = [];
}
