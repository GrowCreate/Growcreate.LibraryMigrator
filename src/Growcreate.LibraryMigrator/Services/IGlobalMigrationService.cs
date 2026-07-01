using Growcreate.LibraryMigrator.Models;

namespace Growcreate.LibraryMigrator.Services;

public interface IGlobalMigrationService
{
    Task<IReadOnlyList<EligibleDocTypeModel>> ListEligibleDocTypesAsync();
    Task<GlobalPreviewReportModel> PreviewByDocTypeAsync(Guid docTypeKey);
    Task<GlobalMigrationRunModel> MigrateByDocTypeAsync(Guid docTypeKey, Guid performingUserKey);
    Task<IReadOnlyList<GlobalMigrationRunModel>> ListRunsAsync();
    Task<MigrationResultModel> RestoreRunAsync(Guid runId, Guid performingUserKey);
}
