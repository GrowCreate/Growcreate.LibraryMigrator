using Growcreate.LibraryMigrator.Models;

namespace Growcreate.LibraryMigrator.Services;

public interface IElementMigrationService
{
    Task<bool> IsApplicableAsync(Guid containerKey);
    Task<PreviewReportModel> PreviewAsync(Guid containerKey);
    Task<MigrationResultModel> MigrateAsync(Guid containerKey, Guid performingUserKey);
    Task<MigrationStatusModel> StatusAsync(Guid containerKey);
    Task<MigrationResultModel> RestoreAsync(Guid containerKey, Guid performingUserKey);

    // Global, doc-type-scoped migration (all instances site-wide). Status/Restore are reused
    // with the doc-type key as the scope key.
    Task<IReadOnlyList<EligibleTypeModel>> GetEligibleTypesAsync();
    Task<PreviewReportModel> PreviewTypeAsync(Guid typeKey);
    Task<MigrationResultModel> MigrateTypeAsync(Guid typeKey, Guid performingUserKey);
}
