using Growcreate.LibraryMigrator.Models;

namespace Growcreate.LibraryMigrator.Services;

public interface IElementMigrationService
{
    Task<bool> IsApplicableAsync(Guid containerKey);
    Task<PreviewReportModel> PreviewAsync(Guid containerKey);
    Task<MigrationResultModel> MigrateAsync(Guid containerKey, Guid performingUserKey);
    Task<MigrationStatusModel> StatusAsync(Guid containerKey);
    Task<MigrationResultModel> RestoreAsync(Guid containerKey, Guid performingUserKey);
}
