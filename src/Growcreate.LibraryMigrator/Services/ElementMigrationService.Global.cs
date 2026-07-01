using System.Text.Json;
using Growcreate.LibraryMigrator.Models;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.ContentPublishing;

namespace Growcreate.LibraryMigrator.Services;

public partial class ElementMigrationService
{
    private const string GlobalRunIndexKey = "Growcreate.LibraryMigrator.GlobalRunIndex";

    private static string GlobalRunKey(Guid runId) => $"Growcreate.LibraryMigrator.GlobalRun.{runId}";

    public Task<IReadOnlyList<EligibleDocTypeModel>> ListEligibleDocTypesAsync()
    {
        List<IContentType> allContentTypes = _contentTypeService.GetAll().ToList();
        var eligible = new List<EligibleDocTypeModel>();

        foreach (IContentType type in allContentTypes)
        {
            if (HasTemplate(type)) continue;

            _contentService.GetPagedOfType(type.Id, 0, 1, out long total, filter: null!);
            if (total == 0) continue;

            eligible.Add(new EligibleDocTypeModel
            {
                Key = type.Key,
                Alias = type.Alias,
                Name = type.Name ?? type.Alias,
                InstanceCount = (int)total,
                IsAlreadyElement = type.IsElement,
                IsAlreadyAllowedInLibrary = type is ContentType ct && ct.AllowedInLibrary,
            });
        }

        return Task.FromResult<IReadOnlyList<EligibleDocTypeModel>>(
            eligible.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public async Task<GlobalPreviewReportModel> PreviewByDocTypeAsync(Guid docTypeKey)
    {
        IContentType? docType = _contentTypeService.Get(docTypeKey);
        if (docType is null)
            return new GlobalPreviewReportModel { DocTypeKey = docTypeKey, Migratable = false };

        IReadOnlyList<IContent> instances = GetAllContentOfType(docType.Id);
        if (instances.Count == 0)
        {
            return new GlobalPreviewReportModel
            {
                DocTypeKey = docType.Key,
                DocTypeAlias = docType.Alias,
                DocTypeName = docType.Name ?? docType.Alias,
                IsAlreadyElement = docType.IsElement,
                IsAlreadyAllowedInLibrary = docType is ContentType ct && ct.AllowedInLibrary,
                Migratable = false,
            };
        }

        int distinctParents = instances
            .Select(i =>
            {
                _documentNavigation.TryGetParentKey(i.Key, out Guid? p);
                return p ?? Guid.Empty;
            })
            .Distinct()
            .Count();

        // Reference-rewrite scope is site-wide across documents, media and members, so
        // preview lists all picker/block properties in exactly the same way as the per-container flow.
        List<IContentType> allContentTypes = _contentTypeService.GetAll().ToList();
        List<IMediaType> mediaTypes = _mediaTypeService.GetAll().ToList();
        List<IMemberType> memberTypes = _memberTypeService.GetAll().ToList();

        var pickerProps = allContentTypes.Cast<IContentTypeComposition>()
            .Concat(mediaTypes)
            .Concat(memberTypes)
            .SelectMany(ct => ct.CompositionPropertyTypes)
            .Where(p => PickerEditorAliases.Contains(p.PropertyEditorAlias))
            .ToList();
        var dataTypeKeys = pickerProps.Select(p => p.DataTypeKey).Distinct().ToArray();
        IEnumerable<IDataType> dataTypes = await _dataTypeService.GetAllAsync(dataTypeKeys);
        Dictionary<Guid, string> dtNames = dataTypes.ToDictionary(dt => dt.Key, dt => dt.Name ?? string.Empty);

        List<PickerPropertyUsage> allPickerUsages = BuildPickerUsages(allContentTypes, dtNames);
        List<PickerPropertyUsage> allBlockEditorUsages = BuildBlockEditorUsages(allContentTypes);
        List<PickerPropertyUsage> mediaPickerUsages = BuildPickerUsages(mediaTypes, dtNames);
        List<PickerPropertyUsage> mediaBlockEditorUsages = BuildBlockEditorUsages(mediaTypes);
        List<PickerPropertyUsage> memberPickerUsages = BuildPickerUsages(memberTypes, dtNames);
        List<PickerPropertyUsage> memberBlockEditorUsages = BuildBlockEditorUsages(memberTypes);

        var warnings = new List<string>();
        if (docType.VariesByCulture())
            warnings.Add($"Type '{docType.Alias}' has culture variation — variant data will be preserved during migration.");

        if (allPickerUsages.Count > 0)
            warnings.Add(
                "Picker data types that reference migrated content will be converted to Element Picker. " +
                "This switches ALL properties using those data types site-wide, including any not listed here.");

        List<string> blockers = FindDescendantBlockers(instances);

        return new GlobalPreviewReportModel
        {
            DocTypeKey = docType.Key,
            DocTypeAlias = docType.Alias,
            DocTypeName = docType.Name ?? docType.Alias,
            InstanceCount = instances.Count,
            DistinctParentCount = distinctParents,
            IsAlreadyElement = docType.IsElement,
            IsAlreadyAllowedInLibrary = docType is ContentType cct && cct.AllowedInLibrary,
            Migratable = blockers.Count == 0,
            AffectedPickers = allPickerUsages,
            AffectedBlockEditors = allBlockEditorUsages,
            AffectedMediaPickers = mediaPickerUsages,
            AffectedMediaBlockEditors = mediaBlockEditorUsages,
            AffectedMemberPickers = memberPickerUsages,
            AffectedMemberBlockEditors = memberBlockEditorUsages,
            Warnings = warnings,
            Blockers = blockers,
        };
    }

    public async Task<GlobalMigrationRunModel> MigrateByDocTypeAsync(Guid docTypeKey, Guid performingUserKey)
    {
        IContentType? docType = _contentTypeService.Get(docTypeKey);
        if (docType is null)
            return FailedRun(Guid.Empty, docTypeKey, string.Empty, string.Empty, "Document type not found.");

        if (HasTemplate(docType))
            return FailedRun(Guid.Empty, docType.Key, docType.Alias, docType.Name ?? docType.Alias,
                "Document type has one or more templates — it is not eligible for migration to the Library.");

        IReadOnlyList<IContent> allInstances = GetAllContentOfType(docType.Id);
        if (allInstances.Count == 0)
            return FailedRun(Guid.Empty, docType.Key, docType.Alias, docType.Name ?? docType.Alias,
                "No instances of this document type were found.");

        List<string> blockers = FindDescendantBlockers(allInstances);
        if (blockers.Count > 0)
            return FailedRun(Guid.Empty, docType.Key, docType.Alias, docType.Name ?? docType.Alias,
                $"Migration refused: {blockers.Count} document(s) have child nodes and cannot be migrated " +
                $"without data loss. Move or remove their children first. Offending nodes: {string.Join("; ", blockers)}");

        Guid runId = Guid.NewGuid();

        Dictionary<Guid, Guid> docParentKeys = allInstances.ToDictionary(
            doc => doc.Key,
            doc =>
            {
                _documentNavigation.TryGetParentKey(doc.Key, out Guid? parentKey);
                return parentKey ?? Guid.Empty;
            });

        var keyMap = new Dictionary<Guid, Guid>();
        var parentFolderKeys = new Dictionary<Guid, Guid>();
        var errors = new List<string>();
        int successCount = 0;

        MigrationResultModel? earlyFailure = await RunScopeAsync(async () =>
        {
            _keyValueService.SetValue(
                $"Growcreate.LibraryMigrator.Snapshot.{runId}",
                BuildSnapshotJson(allInstances, docParentKeys));

            foreach (IContent doc in allInstances)
                _contentService.Delete(doc, Constants.Security.SuperUserId);

            docType.IsElement = true;
            if (docType is ContentType ct) ct.AllowedInLibrary = true;
            await _contentTypeService.UpdateAsync(docType, performingUserKey);

            var distinctParentKeys = docParentKeys.Values.Distinct().ToList();
            foreach (Guid parentKey in distinctParentKeys)
            {
                string folderName = ResolveParentName(parentKey);
                var folderAttempt = await _elementContainerService.CreateAsync(
                    null, folderName, null, performingUserKey);

                if (!folderAttempt.Success)
                    throw new InvalidOperationException(
                        $"Failed to create Library folder '{folderName}': {folderAttempt.Status}");

                parentFolderKeys[parentKey] = folderAttempt.Result!.Key;
            }

            foreach (IContent doc in allInstances)
            {
                Guid originalParentKey = docParentKeys[doc.Key];
                Guid folderKey = parentFolderKeys[originalParentKey];

                ElementCreateModel createModel = BuildElementCreateModel(doc, folderKey);
                var createAttempt = await _elementEditingService.CreateAsync(createModel, performingUserKey);

                if (!createAttempt.Success)
                {
                    errors.Add($"'{doc.Name}': create failed ({createAttempt.Status})");
                    continue;
                }

                Guid newKey = createAttempt.Result!.Content!.Key;
                keyMap[doc.Key] = newKey;

                List<CulturePublishScheduleModel> publishCultures = doc.ContentType.VariesByCulture()
                    ? doc.AvailableCultures.Select(c => new CulturePublishScheduleModel { Culture = c, Schedule = null }).ToList()
                    : [new CulturePublishScheduleModel { Culture = null, Schedule = null }];

                var publishAttempt = await _elementPublishingService.PublishAsync(
                    newKey, publishCultures, performingUserKey);

                if (!publishAttempt.Success)
                {
                    errors.Add($"'{doc.Name}': element created but publish failed ({publishAttempt.Status}).");
                    continue;
                }

                successCount++;
            }

            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("; ", errors));

            _keyValueService.SetValue(
                $"Growcreate.LibraryMigrator.KeyMap.{runId}",
                JsonSerializer.Serialize(keyMap));

            await _auditService.AddAsync(
                AuditType.Custom, performingUserKey, -1, "element-migration",
                $"Global migration: {successCount} '{docType.Alias}' document(s) migrated to Library elements (run {runId})",
                string.Empty);
        });

        if (earlyFailure is not null)
            return FailedRun(runId, docType.Key, docType.Alias, docType.Name ?? docType.Alias,
                earlyFailure.Errors.FirstOrDefault() ?? "Migration failed.");

        var journal = new ReverseJournal();
        journal.FolderKeys.AddRange(parentFolderKeys.Values);

        var detectDomains = BuildContentDomains();
        HashSet<Guid> affectedDataTypeKeys = CollectAffectedPickerDataTypes(keyMap, detectDomains);
        affectedDataTypeKeys.UnionWith(CollectAffectedBlockPickerDataTypes(keyMap, detectDomains));

        var dtRemap = new Dictionary<Guid, Guid>();
        JournalCheckpoint convertCheckpoint = CheckpointJournal(journal);
        int errorsBeforeConvert = errors.Count;
        await RunScopeAsync(async () =>
        {
            (List<string> convertErrors, Dictionary<Guid, Guid> remap) =
                await ConvertPickerDataTypesAsync(affectedDataTypeKeys, BuildContentDomains(), performingUserKey, journal);
            errors.AddRange(convertErrors);
            dtRemap = remap;
        }, swallowAndReport: errors);

        if (errors.Count > errorsBeforeConvert)
        {
            RollbackJournalTo(journal, convertCheckpoint);
            dtRemap = new Dictionary<Guid, Guid>();
        }

        JournalCheckpoint remapCheckpoint = CheckpointJournal(journal);
        int errorsBeforeRemap = errors.Count;
        await RunScopeAsync(async () =>
        {
            var freshDomains = BuildContentDomains();
            var freshTypes = freshDomains.SelectMany(d => d.Types).ToList();

            HashSet<Guid> newDataTypeKeys = [.. dtRemap.Values];
            errors.AddRange(await RemapConvertedPickerValuesAsync(
                keyMap, freshDomains, newDataTypeKeys, performingUserKey, journal));

            errors.AddRange(await RemapBlockPropertiesAsync(
                keyMap, freshDomains, newDataTypeKeys, performingUserKey, journal));

            errors.AddRange(RewriteHistoricalPickerValues(keyMap, freshTypes, newDataTypeKeys, journal));
        }, swallowAndReport: errors);

        if (errors.Count > errorsBeforeRemap)
            RollbackJournalTo(journal, remapCheckpoint);

        _keyValueService.SetValue(
            $"Growcreate.LibraryMigrator.Reverse.{runId}",
            JsonSerializer.Serialize(journal));

        _keyValueService.SetValue(
            ResultKey(runId),
            JsonSerializer.Serialize(new PersistedMigrationResult(successCount, errors)));

        PersistGlobalRunMetadata(runId, docType, DateTime.UtcNow);

        return new GlobalMigrationRunModel
        {
            RunId = runId,
            DocTypeKey = docType.Key,
            DocTypeAlias = docType.Alias,
            DocTypeName = docType.Name ?? docType.Alias,
            MigratedAt = DateTime.UtcNow,
            ElementCount = successCount,
            ErrorCount = errors.Count,
            State = errors.Count > 0 ? MigrationState.PartiallyMigrated : MigrationState.Migrated,
            Errors = errors,
        };
    }

    public Task<IReadOnlyList<GlobalMigrationRunModel>> ListRunsAsync()
    {
        List<Guid> runIds = LoadGlobalRunIndex();
        var runs = new List<GlobalMigrationRunModel>();

        foreach (Guid runId in runIds)
        {
            GlobalRunMetadata? meta = LoadGlobalRunMetadata(runId);
            if (meta is null) continue;

            MigrationStatusModel status = StatusAsync(runId).GetAwaiter().GetResult();
            if (!status.HasMigration) continue;

            runs.Add(new GlobalMigrationRunModel
            {
                RunId = runId,
                DocTypeKey = meta.DocTypeKey,
                DocTypeAlias = meta.DocTypeAlias,
                DocTypeName = meta.DocTypeName,
                MigratedAt = status.MigratedAt,
                ElementCount = status.ElementCount,
                ErrorCount = status.ErrorCount,
                State = status.State,
                Errors = status.Errors,
            });
        }

        return Task.FromResult<IReadOnlyList<GlobalMigrationRunModel>>(
            runs.OrderByDescending(r => r.MigratedAt ?? DateTime.MinValue).ToList());
    }

    public async Task<MigrationResultModel> RestoreRunAsync(Guid runId, Guid performingUserKey)
    {
        MigrationResultModel result = await RestoreAsync(runId, performingUserKey);
        if (result.Success)
            RemoveGlobalRunFromIndex(runId);
        return result;
    }

    private static bool HasTemplate(IContentType type) =>
        type.AllowedTemplates?.Any() == true || type.DefaultTemplate is not null;

    private static GlobalMigrationRunModel FailedRun(
        Guid runId, Guid docTypeKey, string alias, string name, string error) => new()
    {
        RunId = runId,
        DocTypeKey = docTypeKey,
        DocTypeAlias = alias,
        DocTypeName = name,
        MigratedAt = null,
        ElementCount = 0,
        ErrorCount = 1,
        State = MigrationState.NotMigrated,
        Errors = [error],
    };

    private void PersistGlobalRunMetadata(Guid runId, IContentType docType, DateTime migratedAt)
    {
        var meta = new GlobalRunMetadata(
            runId,
            docType.Key,
            docType.Alias,
            docType.Name ?? docType.Alias,
            migratedAt);

        _keyValueService.SetValue(GlobalRunKey(runId), JsonSerializer.Serialize(meta));

        List<Guid> index = LoadGlobalRunIndex();
        if (!index.Contains(runId)) index.Add(runId);
        _keyValueService.SetValue(GlobalRunIndexKey, JsonSerializer.Serialize(index));
    }

    private List<Guid> LoadGlobalRunIndex()
    {
        string? json = _keyValueService.GetValue(GlobalRunIndexKey);
        if (string.IsNullOrEmpty(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private GlobalRunMetadata? LoadGlobalRunMetadata(Guid runId)
    {
        string? json = _keyValueService.GetValue(GlobalRunKey(runId));
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<GlobalRunMetadata>(json, options);
        }
        catch
        {
            return null;
        }
    }

    private void RemoveGlobalRunFromIndex(Guid runId)
    {
        List<Guid> index = LoadGlobalRunIndex();
        if (index.Remove(runId))
            _keyValueService.SetValue(GlobalRunIndexKey, JsonSerializer.Serialize(index));

        _keyValueService.SetValue(GlobalRunKey(runId), string.Empty);
    }

    private sealed record GlobalRunMetadata(
        Guid RunId,
        Guid DocTypeKey,
        string DocTypeAlias,
        string DocTypeName,
        DateTime MigratedAt);
}
