using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Growcreate.LibraryMigrator.Models;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;

namespace Growcreate.LibraryMigrator.Services;

public class ElementMigrationService : IElementMigrationService
{
    private static readonly HashSet<string> PickerEditorAliases =
        ["Umbraco.ContentPicker", "Umbraco.MultiNodeTreePicker"];

    private static readonly HashSet<string> BlockEditorAliases =
        [Constants.PropertyEditors.Aliases.BlockList, Constants.PropertyEditors.Aliases.BlockGrid];

    private const string RichTextEditorAlias = "Umbraco.RichText";

    private static readonly HashSet<string> NestedContainerAliases =
        [Constants.PropertyEditors.Aliases.BlockList, Constants.PropertyEditors.Aliases.BlockGrid, RichTextEditorAlias];

    private static readonly Regex DocumentUdiRegex =
        new(@"umb://document/([0-9a-fA-F]{32})", RegexOptions.Compiled);

    private static readonly Regex ElementUdiRegex =
        new(@"umb://element/([0-9a-fA-F]{32})", RegexOptions.Compiled);

    private const string DataTypeRemapKey = "Growcreate.LibraryMigrator.DataTypeRemap";

    private static string ResultKey(Guid containerKey) => $"Growcreate.LibraryMigrator.Result.{containerKey}";

    private static string ReverseKey(Guid containerKey) => $"Growcreate.LibraryMigrator.Reverse.{containerKey}";

    private readonly IContentService _contentService;
    private readonly IMediaService _mediaService;
    private readonly IMemberService _memberService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IMediaTypeService _mediaTypeService;
    private readonly IMemberTypeService _memberTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IEntityService _entityService;
    private readonly IDocumentNavigationQueryService _documentNavigation;
    private readonly IElementEditingService _elementEditingService;
    private readonly IElementContainerService _elementContainerService;
    private readonly IElementPublishingService _elementPublishingService;
    private readonly IContentPublishingService _contentPublishingService;
    private readonly IKeyValueService _keyValueService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _configSerializer;
    private readonly IAuditService _auditService;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly ILanguageService _languageService;
    private readonly Umbraco.Cms.Infrastructure.Scoping.IScopeAccessor _scopeAccessor;
    private readonly LibraryMigratorSettings _settings;

    public ElementMigrationService(
        IContentService contentService,
        IMediaService mediaService,
        IMemberService memberService,
        IContentTypeService contentTypeService,
        IMediaTypeService mediaTypeService,
        IMemberTypeService memberTypeService,
        IDataTypeService dataTypeService,
        IEntityService entityService,
        IDocumentNavigationQueryService documentNavigation,
        IElementEditingService elementEditingService,
        IElementContainerService elementContainerService,
        IElementPublishingService elementPublishingService,
        IContentPublishingService contentPublishingService,
        IKeyValueService keyValueService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer configSerializer,
        IAuditService auditService,
        ICoreScopeProvider scopeProvider,
        ILanguageService languageService,
        Umbraco.Cms.Infrastructure.Scoping.IScopeAccessor scopeAccessor,
        IOptions<LibraryMigratorSettings> settings)
    {
        _contentService = contentService;
        _mediaService = mediaService;
        _memberService = memberService;
        _contentTypeService = contentTypeService;
        _mediaTypeService = mediaTypeService;
        _memberTypeService = memberTypeService;
        _dataTypeService = dataTypeService;
        _entityService = entityService;
        _documentNavigation = documentNavigation;
        _elementEditingService = elementEditingService;
        _elementContainerService = elementContainerService;
        _elementPublishingService = elementPublishingService;
        _contentPublishingService = contentPublishingService;
        _keyValueService = keyValueService;
        _propertyEditors = propertyEditors;
        _configSerializer = configSerializer;
        _auditService = auditService;
        _scopeProvider = scopeProvider;
        _languageService = languageService;
        _scopeAccessor = scopeAccessor;
        _settings = settings.Value;
    }

    public Task<bool> IsApplicableAsync(Guid containerKey)
    {
        Attempt<int> idAttempt = _entityService.GetId(containerKey, UmbracoObjectTypes.Document);
        if (!idAttempt.Success) return Task.FromResult(false);

        IContent? container = _contentService.GetById(idAttempt.Result);
        if (container is null) return Task.FromResult(false);

        bool applicable = _settings.ContainerDocTypeAliases.Contains(
            container.ContentType.Alias,
            StringComparer.OrdinalIgnoreCase);

        return Task.FromResult(applicable);
    }

    public async Task<PreviewReportModel> PreviewAsync(Guid containerKey)
    {
        Attempt<int> idAttempt = _entityService.GetId(containerKey, UmbracoObjectTypes.Document);
        if (!idAttempt.Success)
            return new PreviewReportModel { Migratable = false, ContainerKey = containerKey };

        IContent? container = _contentService.GetById(idAttempt.Result);
        if (container is null)
            return new PreviewReportModel { Migratable = false, ContainerKey = containerKey };

        if (!_documentNavigation.TryGetChildrenKeys(containerKey, out IEnumerable<Guid> childKeys))
            return new PreviewReportModel { ContainerName = container.Name ?? string.Empty, ContainerKey = containerKey, Migratable = false };

        IReadOnlyList<Guid> childKeyList = childKeys.ToList();
        if (childKeyList.Count == 0)
            return new PreviewReportModel { ContainerName = container.Name ?? string.Empty, ContainerKey = containerKey, Migratable = false };

        var childIntIds = childKeyList
            .Select(k => _entityService.GetId(k, UmbracoObjectTypes.Document))
            .Where(a => a.Success)
            .Select(a => a.Result)
            .ToList();

        IReadOnlyList<IContent> children = _contentService.GetByIds(childIntIds).ToList();
        if (children.Count == 0)
            return new PreviewReportModel { ContainerName = container.Name ?? string.Empty, ContainerKey = containerKey, Migratable = false };

        var childTypeIds = children.Select(c => c.ContentTypeId).Distinct().ToHashSet();
        Dictionary<int, IContentType> allContentTypes = _contentTypeService.GetAll().ToDictionary(ct => ct.Id);

        List<IContentType> childTypes = childTypeIds
            .Select(id => allContentTypes.GetValueOrDefault(id))
            .OfType<IContentType>()
            .ToList();

        List<IMediaType> mediaTypes = _mediaTypeService.GetAll().ToList();
        List<IMemberType> memberTypes = _memberTypeService.GetAll().ToList();

        // Picker data type names span documents, media and members (a data type may be shared).
        var pickerProps = allContentTypes.Values.Cast<IContentTypeComposition>()
            .Concat(mediaTypes)
            .Concat(memberTypes)
            .SelectMany(ct => ct.CompositionPropertyTypes)
            .Where(p => PickerEditorAliases.Contains(p.PropertyEditorAlias))
            .ToList();
        var dataTypeKeys = pickerProps.Select(p => p.DataTypeKey).Distinct().ToArray();
        IEnumerable<IDataType> dataTypes = await _dataTypeService.GetAllAsync(dataTypeKeys);
        Dictionary<Guid, string> dtNames = dataTypes.ToDictionary(dt => dt.Key, dt => dt.Name ?? string.Empty);

        List<PickerPropertyUsage> allPickerUsages = BuildPickerUsages(allContentTypes.Values, dtNames);
        List<PickerPropertyUsage> allBlockEditorUsages = BuildBlockEditorUsages(allContentTypes.Values);
        List<PickerPropertyUsage> mediaPickerUsages = BuildPickerUsages(mediaTypes, dtNames);
        List<PickerPropertyUsage> mediaBlockEditorUsages = BuildBlockEditorUsages(mediaTypes);
        List<PickerPropertyUsage> memberPickerUsages = BuildPickerUsages(memberTypes, dtNames);
        List<PickerPropertyUsage> memberBlockEditorUsages = BuildBlockEditorUsages(memberTypes);

        var typeReports = new List<MigratableTypeReport>();
        var warnings = new List<string>();

        foreach (IContentType childType in childTypes)
        {
            _contentService.GetPagedOfType(childType.Id, 0, 1, out long siteWideTotal, filter: null!);
            int directCount = children.Count(c => c.ContentTypeId == childType.Id);
            bool allowedInLibrary = childType is ContentType ct && ct.AllowedInLibrary;

            typeReports.Add(new MigratableTypeReport
            {
                TypeAlias = childType.Alias,
                TypeName = childType.Name ?? childType.Alias,
                TypeKey = childType.Key,
                IsAlreadyElement = childType.IsElement,
                IsAlreadyAllowedInLibrary = allowedInLibrary,
                DocumentCountSitewide = (int)siteWideTotal,
                DirectChildCount = directCount,
            });

            if (childType.VariesByCulture())
                warnings.Add($"Type '{childType.Alias}' has culture variation — variant data will be preserved during migration.");

            long outsideCount = siteWideTotal - directCount;
            if (outsideCount > 0)
                warnings.Add($"Type '{childType.Alias}' has {outsideCount} instance(s) outside this container. " +
                    $"Only the {directCount} child document(s) under this container will be migrated to the Library. " +
                    "The type is still converted to an element, so the remaining instances become element-type documents and should be reviewed.");
        }

        if (allPickerUsages.Count > 0)
            warnings.Add(
                "Picker data types that reference migrated content will be converted to Element Picker. " +
                "This switches ALL properties using those data types site-wide, including any not listed here.");

        List<string> blockers = FindDescendantBlockers(children);

        return new PreviewReportModel
        {
            ContainerName = container.Name ?? string.Empty,
            ContainerKey = containerKey,
            Migratable = blockers.Count == 0,
            Types = typeReports,
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

    public async Task<MigrationResultModel> MigrateAsync(Guid containerKey, Guid performingUserKey)
    {
        if (!await IsApplicableAsync(containerKey))
            return MigrationResultModel.Fail("Container type is not configured for migration.");

        Attempt<int> idAttempt = _entityService.GetId(containerKey, UmbracoObjectTypes.Document);
        if (!idAttempt.Success) return MigrationResultModel.Fail("Container document not found.");

        IContent? container = _contentService.GetById(idAttempt.Result);
        if (container is null) return MigrationResultModel.Fail("Container document not found.");

        if (!_documentNavigation.TryGetChildrenKeys(containerKey, out IEnumerable<Guid> childKeys))
            return MigrationResultModel.Fail("Could not retrieve container children.");

        var childKeyList = childKeys.ToList();
        if (childKeyList.Count == 0)
            return MigrationResultModel.Fail("No child documents to migrate.");

        var childIntIds = childKeyList
            .Select(k => _entityService.GetId(k, UmbracoObjectTypes.Document))
            .Where(a => a.Success)
            .Select(a => a.Result)
            .ToList();

        var directChildren = _contentService.GetByIds(childIntIds).ToList();
        var childTypeIds = directChildren.Select(c => c.ContentTypeId).Distinct().ToList();

        Dictionary<int, IContentType> allContentTypes = _contentTypeService.GetAll().ToDictionary(ct => ct.Id);
        var childTypes = childTypeIds
            .Select(id => allContentTypes.GetValueOrDefault(id))
            .OfType<IContentType>()
            .ToList();

        if (childTypes.Count == 0)
            return MigrationResultModel.Fail("Could not resolve child content types.");

        // Migrate only the container's direct children — not every site-wide instance of these types.
        List<IContent> allDocsToMigrate = directChildren;

        List<string> blockers = FindDescendantBlockers(allDocsToMigrate);
        if (blockers.Count > 0)
            return MigrationResultModel.Fail(
                $"Migration refused: {blockers.Count} document(s) have child nodes and cannot be migrated " +
                $"without data loss. Move or remove their children first. Offending nodes: {string.Join("; ", blockers)}");

        Dictionary<Guid, Guid> docParentKeys = allDocsToMigrate.ToDictionary(
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
                $"Growcreate.LibraryMigrator.Snapshot.{containerKey}",
                BuildSnapshotJson(allDocsToMigrate, docParentKeys));

            foreach (IContent doc in allDocsToMigrate)
                _contentService.Delete(doc, Constants.Security.SuperUserId);

            foreach (IContentType childType in childTypes)
            {
                childType.IsElement = true;
                if (childType is ContentType ct) ct.AllowedInLibrary = true;
                await _contentTypeService.UpdateAsync(childType, performingUserKey);
            }

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

            foreach (IContent doc in allDocsToMigrate)
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
                $"Growcreate.LibraryMigrator.KeyMap.{containerKey}",
                System.Text.Json.JsonSerializer.Serialize(keyMap));

            await _auditService.AddAsync(
                AuditType.Custom, performingUserKey, -1, "element-migration",
                $"Migrated {successCount} document(s) to Library elements from container {containerKey}",
                string.Empty);
        });

        if (earlyFailure is not null) return earlyFailure;

        var journal = new ReverseJournal();
        journal.FolderKeys.AddRange(parentFolderKeys.Values);

        // Detect affected picker data types across documents, media and members.
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

        // If the conversion phase reported errors it was rolled back: discard its journal entries
        // and the remap so the value-rewrite phase does not reference uncommitted data types.
        if (errors.Count > errorsBeforeConvert)
        {
            RollbackJournalTo(journal, convertCheckpoint);
            dtRemap = new Dictionary<Guid, Guid>();
        }

        JournalCheckpoint remapCheckpoint = CheckpointJournal(journal);
        int errorsBeforeRemap = errors.Count;
        await RunScopeAsync(async () =>
        {
            // Rebuild domains so content types reflect the committed editor conversion.
            var freshDomains = BuildContentDomains();
            var freshTypes = freshDomains.SelectMany(d => d.Types).ToList();

            HashSet<Guid> newDataTypeKeys = [.. dtRemap.Values];
            errors.AddRange(await RemapConvertedPickerValuesAsync(
                keyMap, freshDomains, newDataTypeKeys, performingUserKey, journal));

            errors.AddRange(await RemapBlockPropertiesAsync(
                keyMap, freshDomains, newDataTypeKeys, performingUserKey, journal));

            errors.AddRange(RewriteHistoricalPickerValues(keyMap, freshTypes, newDataTypeKeys, journal));
        }, swallowAndReport: errors);

        // Same contract for the value-rewrite phase: rolled back on error, so its journal entries go too.
        if (errors.Count > errorsBeforeRemap)
            RollbackJournalTo(journal, remapCheckpoint);

        _keyValueService.SetValue(ReverseKey(containerKey), JsonSerializer.Serialize(journal));

        _keyValueService.SetValue(
            ResultKey(containerKey),
            JsonSerializer.Serialize(new PersistedMigrationResult(successCount, errors)));

        return new MigrationResultModel
        {
            Success = true,
            ElementsCreated = successCount,
            LibraryFolderKeys = [.. parentFolderKeys.Values],
            Errors = errors,
        };
    }

    private async Task<MigrationResultModel?> RunScopeAsync(Func<Task> action, List<string>? swallowAndReport = null)
    {
        ICoreScope scope = _scopeProvider.CreateCoreScope();
        MigrationResultModel? failure = null;
        int errorsBefore = swallowAndReport?.Count ?? 0;

        try
        {
            await action();

            // Best-effort phases record soft errors into swallowAndReport instead of throwing.
            // If any were recorded, do NOT complete the scope — roll the whole phase back rather
            // than committing partial state. Callers trim the reverse journal to match so a later
            // restore never tries to revert changes that were rolled back here.
            bool softFailed = swallowAndReport is not null && swallowAndReport.Count > errorsBefore;
            if (!softFailed)
                scope.Complete();
        }
        catch (Exception ex)
        {
            failure = ToFailure(ex, swallowAndReport);
        }

        try
        {
            scope.Dispose();
        }
        catch (Exception ex)
        {
            failure ??= ToFailure(ex, swallowAndReport);
        }

        return failure;
    }

    private static MigrationResultModel? ToFailure(Exception ex, List<string>? swallowAndReport)
    {
        if (swallowAndReport is not null)
        {
            swallowAndReport.Add($"Step failed: {ex.Message}");
            return null;
        }
        return MigrationResultModel.Fail($"Migration failed and was rolled back: {ex.Message}");
    }

    private string ResolveParentName(Guid? parentKey)
    {
        if (parentKey is null) return "Library";
        Attempt<int> idAttempt = _entityService.GetId(parentKey.Value, UmbracoObjectTypes.Document);
        if (!idAttempt.Success) return parentKey.Value.ToString("N")[..8];
        IContent? parent = _contentService.GetById(idAttempt.Result);
        return parent?.Name ?? parentKey.Value.ToString("N")[..8];
    }

    private IReadOnlyList<IContent> GetAllContentOfType(int contentTypeId)
    {
        const int pageSize = 500;
        var result = new List<IContent>();
        long page = 0;
        long total;
        do
        {
            var batch = _contentService.GetPagedOfType(contentTypeId, page, pageSize, out total, null!);
            result.AddRange(batch);
            page++;
        }
        while (result.Count < total);
        return result;
    }

    private IReadOnlyList<IMedia> GetAllMediaOfType(int mediaTypeId)
    {
        const int pageSize = 500;
        var result = new List<IMedia>();
        long page = 0;
        long total;
        do
        {
            var batch = _mediaService.GetPagedOfType(
                mediaTypeId, page, pageSize, out total, null!, Ordering.By("Path"));
            result.AddRange(batch);
            page++;
        }
        while (result.Count < total);
        return result;
    }

    private IReadOnlyList<IMember> GetAllMembersOfType(string memberTypeAlias)
    {
        const int pageSize = 500;
        var result = new List<IMember>();
        long page = 0;
        long total;
        do
        {
            var batch = _memberService.GetAll(
                page, pageSize, out total, "username", Direction.Ascending, true, memberTypeAlias, string.Empty);
            result.AddRange(batch);
            page++;
        }
        while (result.Count < total);
        return result;
    }

    // The three content domains that can hold picker/block references to migrated documents.
    // Reference rewriting (and its rollback) applies uniformly across all three via IContentBase.
    private List<ContentDomain> BuildContentDomains() =>
    [
        new ContentDomain
        {
            ObjectType = "document",
            Types = _contentTypeService.GetAll().Cast<IContentTypeComposition>().ToList(),
            GetItems = t => GetAllContentOfType(t.Id).Cast<IContentBase>().ToList(),
            UpdateTypeAsync = (t, userKey) => _contentTypeService.UpdateAsync((IContentType)t, userKey),
            SaveAsync = async (item, userKey) =>
            {
                var content = (IContent)item;
                bool wasPublished = content.Published;
                _contentService.Save(content, Constants.Security.SuperUserId);
                if (wasPublished) await RepublishAsync(content, userKey);
            },
        },
        new ContentDomain
        {
            ObjectType = "media",
            Types = _mediaTypeService.GetAll().Cast<IContentTypeComposition>().ToList(),
            GetItems = t => GetAllMediaOfType(t.Id).Cast<IContentBase>().ToList(),
            UpdateTypeAsync = (t, userKey) => _mediaTypeService.UpdateAsync((IMediaType)t, userKey),
            SaveAsync = (item, _) =>
            {
                _mediaService.Save((IMedia)item, Constants.Security.SuperUserId);
                return Task.CompletedTask;
            },
        },
        new ContentDomain
        {
            ObjectType = "member",
            Types = _memberTypeService.GetAll().Cast<IContentTypeComposition>().ToList(),
            GetItems = t => GetAllMembersOfType(t.Alias).Cast<IContentBase>().ToList(),
            UpdateTypeAsync = (t, userKey) => _memberTypeService.UpdateAsync((IMemberType)t, userKey),
            SaveAsync = (item, _) =>
            {
                _memberService.Save((IMember)item, Constants.Security.SuperUserId);
                return Task.CompletedTask;
            },
        },
    ];

    private List<string> FindDescendantBlockers(IEnumerable<IContent> docs)
    {
        var blockers = new List<string>();
        foreach (IContent doc in docs)
        {
            if (_documentNavigation.TryGetChildrenKeys(doc.Key, out IEnumerable<Guid> childKeys)
                && childKeys.Any())
            {
                blockers.Add($"{doc.Name ?? doc.Key.ToString()} ({doc.Key})");
            }
        }
        return blockers;
    }

    private static ElementCreateModel BuildElementCreateModel(IContent doc, Guid parentContainerKey)
    {
        var properties = new List<PropertyValueModel>();
        foreach (IProperty prop in doc.Properties)
        {
            foreach (IPropertyValue val in prop.Values)
            {
                properties.Add(new PropertyValueModel
                {
                    Alias = prop.Alias,
                    Value = val.EditedValue,
                    Culture = val.Culture,
                    Segment = val.Segment,
                });
            }
        }

        var variants = new List<VariantModel>();
        if (doc.ContentType.VariesByCulture())
        {
            foreach (string culture in doc.AvailableCultures)
            {
                variants.Add(new VariantModel
                {
                    Culture = culture,
                    Segment = null,
                    Name = doc.GetCultureName(culture) ?? doc.Name ?? string.Empty,
                });
            }
        }
        else
        {
            variants.Add(new VariantModel
            {
                Culture = null,
                Segment = null,
                Name = doc.Name ?? string.Empty,
            });
        }

        return new ElementCreateModel
        {
            ContentTypeKey = doc.ContentType.Key,
            ParentKey = parentContainerKey,
            Properties = properties,
            Variants = variants,
        };
    }

    private static string BuildSnapshotJson(
        IReadOnlyList<IContent> docs,
        Dictionary<Guid, Guid> docParentKeys)
    {
        var snapshot = new
        {
            TakenAt = DateTime.UtcNow,
            Documents = docs.Select(d => new
            {
                d.Key,
                ParentKey = docParentKeys.GetValueOrDefault(d.Key),
                Name = d.Name ?? string.Empty,
                ContentTypeAlias = d.ContentType.Alias,
                CultureNames = d.ContentType.VariesByCulture()
                    ? d.AvailableCultures.ToDictionary(c => c, c => d.GetCultureName(c) ?? string.Empty)
                    : null,
                Properties = d.Properties.SelectMany(p => p.Values.Select(v => new
                {
                    p.Alias,
                    Value = JsonSerializer.Serialize(v.EditedValue),
                    v.Culture,
                    v.Segment,
                })),
            }),
        };

        return JsonSerializer.Serialize(snapshot);
    }

    private async Task<List<string>> RemapConvertedPickerValuesAsync(
        Dictionary<Guid, Guid> keyMap,
        IReadOnlyList<ContentDomain> domains,
        HashSet<Guid> newPickerDataTypeKeys,
        Guid userKey,
        ReverseJournal journal)
    {
        var errors = new List<string>();
        if (newPickerDataTypeKeys.Count == 0) return errors;

        foreach (ContentDomain domain in domains)
        foreach (IContentTypeComposition type in domain.Types)
        {
            var propAliases = type.CompositionPropertyTypes
                .Where(p => newPickerDataTypeKeys.Contains(p.DataTypeKey))
                .Select(p => p.Alias)
                .ToHashSet();

            if (propAliases.Count == 0) continue;

            foreach (IContentBase item in domain.GetItems(type))
            {
                bool changed = false;

                foreach (string alias in propAliases)
                {
                    IProperty? prop = item.Properties.FirstOrDefault(p => p.Alias == alias);
                    if (prop is null) continue;

                    foreach (IPropertyValue val in prop.Values)
                    {
                        if (val.EditedValue is not string strVal) continue;

                        journal.PropertyValues.Add(new PropertyValueEntry(
                            item.Key, domain.ObjectType, alias, val.Culture, val.Segment, strVal));

                        if (strVal.Length == 0)
                        {
                            item.SetValue(alias, "[]", val.Culture, val.Segment);
                            changed = true;
                            continue;
                        }

                        string rewritten = BuildElementPickerValue(strVal, keyMap, out bool hit);
                        if (!hit)
                        {
                            errors.Add(
                                $"'{item.Name}' ({item.Key}) property '{alias}': reference(s) to " +
                                "non-migrated content were cleared — Element Picker can only reference elements.");
                        }

                        item.SetValue(alias, rewritten, val.Culture, val.Segment);
                        changed = true;
                    }
                }

                if (!changed) continue;

                try
                {
                    await domain.SaveAsync(item, userKey);
                }
                catch (Exception ex)
                {
                    errors.Add($"Picker value rewrite failed for '{item.Name}' ({item.Key}): {ex.Message}");
                }
            }
        }

        return errors;
    }

    private HashSet<Guid> CollectAffectedBlockPickerDataTypes(
        Dictionary<Guid, Guid> keyMap,
        IReadOnlyList<ContentDomain> domains)
    {
        var affected = new HashSet<Guid>();
        if (keyMap.Count == 0) return affected;

        // Block items reference element content types (always documents), regardless of which
        // domain owns the container property — resolve them from the content type service.
        Dictionary<Guid, IContentType> typesByKey = _contentTypeService.GetAll().ToDictionary(t => t.Key);

        bool CollectLeaf(IContentType _, JsonObject valueObj, IPropertyType prop)
        {
            if (!PickerEditorAliases.Contains(prop.PropertyEditorAlias)) return false;
            if (affected.Contains(prop.DataTypeKey)) return false;
            string? valStr = GetNodeStringCaseInsensitive(valueObj, "value");
            if (valStr is null) return false;
            if (ExtractSourceDocumentKeys(valStr).Any(keyMap.ContainsKey))
                affected.Add(prop.DataTypeKey);
            return false;
        }

        foreach (ContentDomain domain in domains)
        foreach (IContentTypeComposition type in domain.Types)
        {
            var containerProps = type.CompositionPropertyTypes
                .Where(p => NestedContainerAliases.Contains(p.PropertyEditorAlias))
                .Select(p => (p.Alias, Editor: p.PropertyEditorAlias))
                .ToList();

            if (containerProps.Count == 0) continue;

            foreach (IContentBase item in domain.GetItems(type))
            {
                foreach ((string alias, string editor) in containerProps)
                {
                    IProperty? prop = item.Properties.FirstOrDefault(p => p.Alias == alias);
                    if (prop is null) continue;

                    foreach (IPropertyValue val in prop.Values)
                    {
                        if (val.EditedValue is not string strVal || strVal.Length == 0) continue;

                        JsonNode? root = TryParseBlockJson(strVal);
                        if (root is null) continue;

                        JsonNode? blockValueNode = GetBlockValueRoot(root, editor);
                        if (blockValueNode is null) continue;

                        WalkBlockValue(blockValueNode, typesByKey, CollectLeaf);
                    }
                }
            }
        }

        return affected;
    }

    private async Task<List<string>> RemapBlockPropertiesAsync(
        Dictionary<Guid, Guid> keyMap,
        IReadOnlyList<ContentDomain> domains,
        HashSet<Guid> newPickerDataTypeKeys,
        Guid userKey,
        ReverseJournal journal)
    {
        var errors = new List<string>();
        if (keyMap.Count == 0) return errors;

        // Block items reference element content types (always documents), regardless of which
        // domain owns the container property — resolve them from the content type service.
        Dictionary<Guid, IContentType> typesByKey = _contentTypeService.GetAll().ToDictionary(t => t.Key);

        foreach (ContentDomain domain in domains)
        foreach (IContentTypeComposition type in domain.Types)
        {
            var containerProps = type.CompositionPropertyTypes
                .Where(p => NestedContainerAliases.Contains(p.PropertyEditorAlias))
                .Select(p => (p.Alias, Editor: p.PropertyEditorAlias))
                .ToList();

            if (containerProps.Count == 0) continue;

            foreach (IContentBase item in domain.GetItems(type))
            {
                bool changed = false;

                bool RemapLeaf(IContentType elementType, JsonObject valueObj, IPropertyType prop)
                {
                    string? valStr = GetNodeStringCaseInsensitive(valueObj, "value");
                    if (valStr is null) return false;

                    bool isConvertedPicker = prop.PropertyEditorAlias == "Umbraco.ElementPicker"
                        && newPickerDataTypeKeys.Contains(prop.DataTypeKey);

                    if (isConvertedPicker)
                    {
                        string rewritten;
                        if (valStr.Length == 0)
                        {
                            rewritten = "[]";
                        }
                        else
                        {
                            rewritten = BuildElementPickerValue(valStr, keyMap, out bool refFound);
                            if (!refFound)
                            {
                                errors.Add(
                                    $"'{item.Name}' ({item.Key}) nested block property '{prop.Alias}': " +
                                    "reference(s) to non-migrated content were cleared — Element Picker can only reference elements.");
                            }
                        }
                        SetBlockValueString(valueObj, rewritten);
                        return true;
                    }

                    string swapped = RemapUdis(valStr, keyMap, out bool hit);
                    if (!hit) return false;
                    SetBlockValueString(valueObj, swapped);
                    return true;
                }

                foreach ((string alias, string editor) in containerProps)
                {
                    IProperty? prop = item.Properties.FirstOrDefault(p => p.Alias == alias);
                    if (prop is null) continue;

                    foreach (IPropertyValue val in prop.Values)
                    {
                        if (val.EditedValue is not string strVal || strVal.Length == 0) continue;

                        JsonNode? root = TryParseBlockJson(strVal);
                        if (root is null) continue;

                        JsonNode? blockValueNode = GetBlockValueRoot(root, editor);
                        if (blockValueNode is null) continue;

                        if (!WalkBlockValue(blockValueNode, typesByKey, RemapLeaf)) continue;

                        journal.PropertyValues.Add(new PropertyValueEntry(
                            item.Key, domain.ObjectType, alias, val.Culture, val.Segment, strVal));

                        item.SetValue(alias, root.ToJsonString(), val.Culture, val.Segment);
                        changed = true;
                    }
                }

                if (!changed) continue;

                try
                {
                    await domain.SaveAsync(item, userKey);
                }
                catch (Exception ex)
                {
                    errors.Add($"Block remap failed for '{item.Name}' ({item.Key}): {ex.Message}");
                }
            }
        }

        return errors;
    }

    private static JsonNode? GetBlockValueRoot(JsonNode parsedValue, string editorAlias) =>
        editorAlias == RichTextEditorAlias
            ? GetNodeCaseInsensitive(parsedValue, "blocks")
            : parsedValue;

    private bool WalkBlockValue(
        JsonNode blockValue,
        Dictionary<Guid, IContentType> typesByKey,
        Func<IContentType, JsonObject, IPropertyType, bool> handleLeaf)
    {
        bool changed = false;

        foreach (JsonObject item in EnumerateBlockItems(blockValue))
        {
            if (!TryGetElementType(item, typesByKey, out IContentType? elementType)) continue;

            foreach (JsonObject valueObj in EnumerateBlockValues(item))
            {
                string? alias = GetStringCaseInsensitive(valueObj, "alias");
                if (alias is null) continue;

                IPropertyType? prop = elementType.CompositionPropertyTypes.FirstOrDefault(p => p.Alias == alias);
                if (prop is null) continue;

                if (NestedContainerAliases.Contains(prop.PropertyEditorAlias))
                    changed |= RecurseNestedValue(valueObj, prop.PropertyEditorAlias, typesByKey, handleLeaf);
                else
                    changed |= handleLeaf(elementType, valueObj, prop);
            }
        }

        return changed;
    }

    private bool RecurseNestedValue(
        JsonObject valueObj,
        string editorAlias,
        Dictionary<Guid, IContentType> typesByKey,
        Func<IContentType, JsonObject, IPropertyType, bool> handleLeaf)
    {
        JsonNode? valueNode = GetNodeCaseInsensitive(valueObj, "value");
        if (valueNode is null) return false;

        bool wasString = valueNode is JsonValue jv && jv.TryGetValue(out string? _);
        JsonNode? inner;
        if (wasString)
        {
            string s = valueNode.GetValue<string>();
            if (string.IsNullOrEmpty(s)) return false;
            inner = TryParseBlockJson(s);
        }
        else
        {
            inner = valueNode;
        }
        if (inner is null) return false;

        JsonNode? blockValueNode = GetBlockValueRoot(inner, editorAlias);
        if (blockValueNode is null) return false;

        bool changed = WalkBlockValue(blockValueNode, typesByKey, handleLeaf);

        if (changed && wasString)
            SetBlockValueString(valueObj, inner.ToJsonString());

        return changed;
    }

    private static JsonNode? TryParseBlockJson(string value)
    {
        try { return JsonNode.Parse(value); }
        catch (JsonException) { return null; }
    }

    private static IEnumerable<JsonObject> EnumerateBlockItems(JsonNode root)
    {
        foreach (string arrayName in new[] { "contentData", "settingsData" })
        {
            JsonNode? array = GetNodeCaseInsensitive(root, arrayName);
            if (array is not JsonArray jsonArray) continue;
            foreach (JsonNode? item in jsonArray)
                if (item is JsonObject obj) yield return obj;
        }
    }

    private static IEnumerable<JsonObject> EnumerateBlockValues(JsonObject itemObj)
    {
        JsonNode? values = GetNodeCaseInsensitive(itemObj, "values");
        if (values is not JsonArray valuesArray) yield break;
        foreach (JsonNode? v in valuesArray)
            if (v is JsonObject obj) yield return obj;
    }

    private static bool TryGetElementType(
        JsonObject itemObj, Dictionary<Guid, IContentType> typesByKey, [NotNullWhen(true)] out IContentType? elementType)
    {
        elementType = null;
        string? ctk = GetStringCaseInsensitive(itemObj, "contentTypeKey");
        return ctk is not null
            && Guid.TryParse(ctk, out Guid ctKey)
            && typesByKey.TryGetValue(ctKey, out elementType);
    }

    private static void SetBlockValueString(JsonObject valueObj, string newValue)
    {
        foreach (string key in new[] { "value", "Value" })
        {
            if (valueObj.ContainsKey(key)) { valueObj[key] = JsonValue.Create(newValue); return; }
        }
    }

    private static JsonNode? GetNodeCaseInsensitive(JsonNode node, string propName)
    {
        if (node is not JsonObject obj) return null;
        foreach (KeyValuePair<string, JsonNode?> kvp in obj)
            if (string.Equals(kvp.Key, propName, StringComparison.OrdinalIgnoreCase)) return kvp.Value;
        return null;
    }

    private static string? GetStringCaseInsensitive(JsonObject obj, string propName) =>
        GetNodeCaseInsensitive(obj, propName) is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    private static string? GetNodeStringCaseInsensitive(JsonObject obj, string propName)
    {
        JsonNode? node = GetNodeCaseInsensitive(obj, propName);
        return node switch
        {
            null => null,
            JsonValue v when v.TryGetValue(out string? s) => s,
            _ => node.ToJsonString(),
        };
    }

    private HashSet<Guid> CollectAffectedPickerDataTypes(
        Dictionary<Guid, Guid> keyMap,
        IReadOnlyList<ContentDomain> domains)
    {
        var affected = new HashSet<Guid>();
        if (keyMap.Count == 0) return affected;

        foreach (ContentDomain domain in domains)
        foreach (IContentTypeComposition type in domain.Types)
        {
            var propMap = type.CompositionPropertyTypes
                .Where(p => PickerEditorAliases.Contains(p.PropertyEditorAlias))
                .ToDictionary(p => p.Alias, p => p.DataTypeKey);

            if (propMap.Count == 0) continue;

            foreach (IContentBase item in domain.GetItems(type))
            {
                foreach ((string alias, Guid dtKey) in propMap)
                {
                    if (affected.Contains(dtKey)) continue;

                    IProperty? prop = item.Properties.FirstOrDefault(p => p.Alias == alias);
                    if (prop is null) continue;

                    foreach (IPropertyValue val in prop.Values)
                    {
                        if (val.EditedValue is not string strVal || strVal.Length == 0) continue;

                        bool hit = ExtractSourceDocumentKeys(strVal).Any(keyMap.ContainsKey);

                        if (hit) { affected.Add(dtKey); break; }
                    }
                }
            }
        }

        return affected;
    }

    private static string BuildElementPickerValue(string value, IReadOnlyDictionary<Guid, Guid> keyMap, out bool changed)
    {
        var newKeys = new List<string>();
        var seen = new HashSet<Guid>();

        foreach (Guid oldKey in ExtractSourceDocumentKeys(value))
        {
            if (keyMap.TryGetValue(oldKey, out Guid newKey) && seen.Add(newKey))
                newKeys.Add(newKey.ToString());
        }

        changed = newKeys.Count > 0;
        return JsonSerializer.Serialize(newKeys);
    }

    private sealed class PickerDataRow
    {
        public int Id { get; set; }
        public string? TextValue { get; set; }
    }

    private List<string> RewriteHistoricalPickerValues(
        Dictionary<Guid, Guid> keyMap,
        IReadOnlyList<IContentTypeComposition> freshDocTypes,
        HashSet<Guid> newPickerDataTypeKeys,
        ReverseJournal journal)
    {
        var errors = new List<string>();
        if (newPickerDataTypeKeys.Count == 0) return errors;

        var db = _scopeAccessor.AmbientScope?.Database;
        if (db is null)
        {
            errors.Add("No ambient database scope available — historical version cleanup skipped.");
            return errors;
        }

        var propTypeIds = freshDocTypes
            .SelectMany(t => t.PropertyTypes)
            .Where(p => newPickerDataTypeKeys.Contains(p.DataTypeKey))
            .Select(p => p.Id)
            .Distinct()
            .ToList();

        foreach (int ptId in propTypeIds)
        {
            List<PickerDataRow> rows;
            try
            {
                rows = db.Fetch<PickerDataRow>(
                    "SELECT id AS Id, textValue AS TextValue FROM umbracoPropertyData " +
                    "WHERE propertyTypeId = @0 AND textValue IS NOT NULL", ptId);
            }
            catch (Exception ex)
            {
                errors.Add($"Historical read failed for property type {ptId}: {ex.Message}");
                continue;
            }

            foreach (PickerDataRow row in rows)
            {
                if (row.TextValue is null || !IsLegacyPickerValue(row.TextValue)) continue;

                string newVal = NormalizeHistoricalPickerValue(row.TextValue, keyMap);
                if (newVal == row.TextValue) continue;

                journal.HistoricalRows.Add(new HistoricalRowEntry(row.Id, row.TextValue));

                try
                {
                    db.Execute("UPDATE umbracoPropertyData SET textValue = @0 WHERE id = @1", newVal, row.Id);
                }
                catch (Exception ex)
                {
                    errors.Add($"Historical rewrite failed for property data row {row.Id}: {ex.Message}");
                }
            }
        }

        return errors;
    }

    private static bool IsLegacyPickerValue(string value)
    {
        string s = value.TrimStart();
        if (s.StartsWith("umb://", StringComparison.OrdinalIgnoreCase)) return true;
        if (!s.StartsWith('[')) return false;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
            foreach (JsonElement el in doc.RootElement.EnumerateArray())
                return el.ValueKind == JsonValueKind.Object;
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string NormalizeHistoricalPickerValue(string value, IReadOnlyDictionary<Guid, Guid> keyMap)
    {
        var keys = new List<string>();
        var seen = new HashSet<Guid>();

        foreach (Match m in ElementUdiRegex.Matches(value))
            if (Guid.TryParse(m.Groups[1].Value, out Guid g) && seen.Add(g))
                keys.Add(g.ToString());

        foreach (Guid oldKey in ExtractSourceDocumentKeys(value))
            if (keyMap.TryGetValue(oldKey, out Guid newKey) && seen.Add(newKey))
                keys.Add(newKey.ToString());

        return JsonSerializer.Serialize(keys);
    }

    private static List<Guid> ExtractSourceDocumentKeys(string value)
    {
        var keys = new List<Guid>();

        if (value.Length > 0 && value[0] == '[')
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(value);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind != JsonValueKind.Object) continue;
                        string? type = GetStringCaseInsensitive(el, "type");
                        if (!string.Equals(type, "document", StringComparison.OrdinalIgnoreCase)) continue;
                        string? unique = GetStringCaseInsensitive(el, "unique");
                        if (Guid.TryParse(unique, out Guid g)) keys.Add(g);
                    }
                    return keys;
                }
            }
            catch (JsonException)
            {
            }
        }

        foreach (Match m in DocumentUdiRegex.Matches(value))
            if (Guid.TryParse(m.Groups[1].Value, out Guid key)) keys.Add(key);

        return keys;
    }

    private static string? GetStringCaseInsensitive(JsonElement obj, string propName)
    {
        foreach (JsonProperty p in obj.EnumerateObject())
            if (string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase))
                return p.Value.GetString();
        return null;
    }

    private static string RemapUdis(string value, IReadOnlyDictionary<Guid, Guid> keyMap, out bool changed)
    {
        bool any = false;
        string result = DocumentUdiRegex.Replace(value, m =>
        {
            if (Guid.TryParse(m.Groups[1].Value, out Guid oldKey) && keyMap.TryGetValue(oldKey, out Guid newKey))
            {
                any = true;
                return new GuidUdi(Constants.UdiEntityType.Element, newKey).ToString();
            }
            return m.Value;
        });
        changed = any;
        return result;
    }

    private async Task RepublishAsync(IContent content, Guid userKey)
    {
        List<CulturePublishScheduleModel> cultures = content.ContentType.VariesByCulture()
            ? content.PublishedCultures.Select(c => new CulturePublishScheduleModel { Culture = c, Schedule = null }).ToList()
            : [new CulturePublishScheduleModel { Culture = null, Schedule = null }];

        if (cultures.Count == 0) return;
        await _contentPublishingService.PublishAsync(content.Key, cultures, userKey);
    }

    private async Task<(List<string> errors, Dictionary<Guid, Guid> dataTypeKeyRemap)> ConvertPickerDataTypesAsync(
        HashSet<Guid> affectedDataTypeKeys,
        IReadOnlyList<ContentDomain> domains,
        Guid performingUserKey,
        ReverseJournal journal)
    {
        var errors = new List<string>();
        var emptyRemap = new Dictionary<Guid, Guid>();
        if (affectedDataTypeKeys.Count == 0) return (errors, emptyRemap);

        if (!_propertyEditors.TryGet("Umbraco.ElementPicker", out IDataEditor? elementPickerEditor))
        {
            errors.Add("Could not resolve Umbraco.ElementPicker property editor — data type conversion skipped.");
            return (errors, emptyRemap);
        }

        Dictionary<Guid, Guid> persisted = LoadDataTypeRemap();

        IEnumerable<IDataType> oldDataTypes = await _dataTypeService.GetAllAsync([.. affectedDataTypeKeys]);
        var dataTypeKeyRemap = new Dictionary<Guid, Guid>();
        var newKeyToId = new Dictionary<Guid, int>();

        foreach (IDataType oldDt in oldDataTypes)
        {
            if (persisted.TryGetValue(oldDt.Key, out Guid existingKey)
                && await _dataTypeService.GetAsync(existingKey) is { } existingDt)
            {
                if (existingDt.EditorUiAlias != "Umb.PropertyEditorUi.ElementPicker")
                {
                    existingDt.EditorUiAlias = "Umb.PropertyEditorUi.ElementPicker";
                    await _dataTypeService.UpdateAsync(existingDt, performingUserKey);
                }
                dataTypeKeyRemap[oldDt.Key] = existingKey;
                newKeyToId[existingKey] = existingDt.Id;
                continue;
            }

            var newDt = new DataType(elementPickerEditor!, _configSerializer, -1)
            {
                Name = $"{oldDt.Name} (Element Picker)",
                EditorUiAlias = "Umb.PropertyEditorUi.ElementPicker",
                DatabaseType = oldDt.DatabaseType,
                ConfigurationData = BuildElementPickerConfig(oldDt),
            };

            var createAttempt = await _dataTypeService.CreateAsync(newDt, performingUserKey);
            if (!createAttempt.Success)
            {
                errors.Add($"Failed to create ElementPicker data type for '{oldDt.Name}': {createAttempt.Status}");
                continue;
            }

            Guid newKey = createAttempt.Result!.Key;
            dataTypeKeyRemap[oldDt.Key] = newKey;
            newKeyToId[newKey] = createAttempt.Result.Id;
            persisted[oldDt.Key] = newKey;
            journal.CreatedDataTypeKeys.Add(newKey);
        }

        SaveDataTypeRemap(persisted);

        if (dataTypeKeyRemap.Count == 0) return (errors, dataTypeKeyRemap);

        // Repoint every property that uses a converted data type, across all domains. A picker data
        // type can be shared between document, media and member types, so all three must switch editor.
        foreach (ContentDomain domain in domains)
        {
            var changedOwnerKeys = new HashSet<Guid>();

            foreach (IContentTypeComposition ct in domain.Types)
            {
                bool changed = false;
                foreach (IPropertyType prop in ct.PropertyTypes)
                {
                    if (!dataTypeKeyRemap.TryGetValue(prop.DataTypeKey, out Guid newDtKey))
                        continue;
                    journal.DataTypeConversions.Add(new DataTypeConversionEntry(
                        ct.Key, prop.Alias, prop.DataTypeKey, prop.DataTypeId, prop.PropertyEditorAlias));
                    prop.DataTypeId = newKeyToId[newDtKey];
                    prop.DataTypeKey = newDtKey;
                    prop.PropertyEditorAlias = "Umbraco.ElementPicker";
                    changed = true;
                }
                if (!changed) continue;

                try
                {
                    await domain.UpdateTypeAsync(ct, performingUserKey);
                    changedOwnerKeys.Add(ct.Key);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to update content type '{ct.Alias}' after data type conversion: {ex.Message}");
                }
            }

            if (changedOwnerKeys.Count == 0) continue;

            foreach (IContentTypeComposition consumer in domain.Types)
            {
                if (changedOwnerKeys.Contains(consumer.Key)) continue;
                bool composesChangedOwner = consumer.ContentTypeComposition
                    .Any(composed => changedOwnerKeys.Contains(composed.Key));
                if (!composesChangedOwner) continue;

                try
                {
                    await domain.UpdateTypeAsync(consumer, performingUserKey);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to refresh composing content type '{consumer.Alias}': {ex.Message}");
                }
            }
        }

        return (errors, dataTypeKeyRemap);
    }

    private static Dictionary<string, object> BuildElementPickerConfig(IDataType oldDt)
    {
        var config = new Dictionary<string, object>();

        if (oldDt.EditorAlias == "Umbraco.ContentPicker")
        {
            config["validationLimit"] = new Dictionary<string, object> { ["min"] = 0, ["max"] = 1 };
        }
        else if (oldDt.EditorAlias == "Umbraco.MultiNodeTreePicker")
        {
            int min = GetConfigInt(oldDt.ConfigurationData, "minNumber");
            int max = GetConfigInt(oldDt.ConfigurationData, "maxNumber");
            if (min > 0 || max > 0)
            {
                config["validationLimit"] = new Dictionary<string, object>
                {
                    ["min"] = min,
                    ["max"] = max > 0 ? max : int.MaxValue,
                };
            }
        }

        return config;
    }

    private static int GetConfigInt(IDictionary<string, object> config, string key)
    {
        if (!config.TryGetValue(key, out object? raw) || raw is null) return 0;
        return raw switch
        {
            int i => i,
            long l => (int)l,
            JsonElement je when je.TryGetInt32(out int v) => v,
            string s when int.TryParse(s, out int v) => v,
            _ => 0,
        };
    }

    private Dictionary<Guid, Guid> LoadDataTypeRemap()
    {
        string? json = _keyValueService.GetValue(DataTypeRemapKey);
        return string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(json) ?? [];
    }

    private void SaveDataTypeRemap(Dictionary<Guid, Guid> map) =>
        _keyValueService.SetValue(DataTypeRemapKey, JsonSerializer.Serialize(map));

    private static List<PickerPropertyUsage> BuildUsages(
        IEnumerable<IContentTypeComposition> types,
        HashSet<string> editorAliases,
        Dictionary<Guid, string>? dtNames = null)
    {
        var usages = new List<PickerPropertyUsage>();
        var seen = new HashSet<string>();

        foreach (IContentTypeComposition type in types)
        {
            foreach (IPropertyType prop in type.CompositionPropertyTypes)
            {
                if (!editorAliases.Contains(prop.PropertyEditorAlias))
                    continue;

                string dedupeKey = $"{type.Alias}:{prop.Alias}";
                if (!seen.Add(dedupeKey))
                    continue;

                usages.Add(new PickerPropertyUsage
                {
                    PropertyAlias = prop.Alias,
                    PropertyName = prop.Name ?? prop.Alias,
                    OwnerDocTypeAlias = type.Alias,
                    OwnerDocTypeName = type.Name ?? type.Alias,
                    PickerEditorAlias = prop.PropertyEditorAlias,
                    DataTypeName = dtNames?.GetValueOrDefault(prop.DataTypeKey) ?? string.Empty,
                });
            }
        }

        return usages;
    }

    private static List<PickerPropertyUsage> BuildPickerUsages(
        IEnumerable<IContentTypeComposition> types,
        Dictionary<Guid, string>? dtNames = null) =>
        BuildUsages(types, PickerEditorAliases, dtNames);

    private static List<PickerPropertyUsage> BuildBlockEditorUsages(IEnumerable<IContentTypeComposition> types) =>
        BuildUsages(types, BlockEditorAliases);

    public Task<MigrationStatusModel> StatusAsync(Guid containerKey)
    {
        string? keyMapJson = _keyValueService.GetValue($"Growcreate.LibraryMigrator.KeyMap.{containerKey}");
        if (string.IsNullOrEmpty(keyMapJson))
            return Task.FromResult(new MigrationStatusModel { HasMigration = false });

        var keyMap = JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(keyMapJson);
        if (keyMap is null || keyMap.Count == 0)
            return Task.FromResult(new MigrationStatusModel { HasMigration = false });

        DateTime? migratedAt = null;
        string? snapshotJson = _keyValueService.GetValue($"Growcreate.LibraryMigrator.Snapshot.{containerKey}");
        if (!string.IsNullOrEmpty(snapshotJson))
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            if (doc.RootElement.TryGetProperty("TakenAt", out JsonElement takenAt))
                migratedAt = takenAt.GetDateTime();
        }

        List<string> errors = [];
        string? resultJson = _keyValueService.GetValue(ResultKey(containerKey));
        if (!string.IsNullOrEmpty(resultJson))
        {
            PersistedMigrationResult? persisted =
                JsonSerializer.Deserialize<PersistedMigrationResult>(resultJson);
            if (persisted?.Errors is { Count: > 0 })
                errors = persisted.Errors;
        }

        return Task.FromResult(new MigrationStatusModel
        {
            HasMigration = true,
            State = errors.Count > 0 ? MigrationState.PartiallyMigrated : MigrationState.Migrated,
            MigratedAt = migratedAt,
            ElementCount = keyMap.Count,
            ErrorCount = errors.Count,
            Errors = errors,
        });
    }

    public async Task<MigrationResultModel> RestoreAsync(Guid containerKey, Guid performingUserKey)
    {
        string? keyMapJson = _keyValueService.GetValue($"Growcreate.LibraryMigrator.KeyMap.{containerKey}");
        if (string.IsNullOrEmpty(keyMapJson))
            return MigrationResultModel.Fail("No migration key map found — nothing to restore.");

        var keyMap = JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(keyMapJson);
        if (keyMap is null || keyMap.Count == 0)
            return MigrationResultModel.Fail("Key map is empty — nothing to restore.");

        string? snapshotJson = _keyValueService.GetValue($"Growcreate.LibraryMigrator.Snapshot.{containerKey}");
        if (string.IsNullOrEmpty(snapshotJson))
            return MigrationResultModel.Fail("No snapshot found — cannot restore document property values.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        SnapshotRoot? snapshot = JsonSerializer.Deserialize<SnapshotRoot>(snapshotJson, options);
        if (snapshot is null)
            return MigrationResultModel.Fail("Snapshot data is invalid.");

        var errors = new List<string>();

        ReverseJournal? journal = null;
        string? reverseJson = _keyValueService.GetValue(ReverseKey(containerKey));
        if (!string.IsNullOrEmpty(reverseJson))
        {
            try { journal = JsonSerializer.Deserialize<ReverseJournal>(reverseJson, options); }
            catch (Exception ex)
            {
                errors.Add($"Reverse journal unreadable — forward transforms will not be reverted: {ex.Message}");
            }
        }

        ICoreScope scope = _scopeProvider.CreateCoreScope();
        MigrationResultModel result;
        try
        {
        // 1. Revert data-type conversions (switch the editor back) BEFORE writing legacy values back,
        //    so reference tracking on save runs against the original ContentPicker/MNTP editor.
        if (journal is not null)
            await RevertDataTypeConversionsAsync(journal, errors, performingUserKey);

        // 2. Delete the Library elements created by the migration.
        foreach (Guid elementKey in keyMap.Values)
        {
            try
            {
                await _elementEditingService.DeleteAsync(elementKey, performingUserKey);
            }
            catch (Exception ex)
            {
                errors.Add($"Could not delete element {elementKey}: {ex.Message}");
            }
        }

        // 3. Un-flip IsElement / AllowedInLibrary on the migrated types.
        var allContentTypes = _contentTypeService.GetAll().ToDictionary(ct => ct.Alias);
        var distinctTypeAliases = snapshot.Documents.Select(d => d.ContentTypeAlias).Distinct();

        foreach (string typeAlias in distinctTypeAliases)
        {
            if (!allContentTypes.TryGetValue(typeAlias, out IContentType? ct))
            {
                errors.Add($"Content type '{typeAlias}' not found — cannot un-flip IsElement.");
                continue;
            }

            ct.IsElement = false;
            if (ct is ContentType concreteType) concreteType.AllowedInLibrary = false;

            try { await _contentTypeService.UpdateAsync(ct, performingUserKey); }
            catch (Exception ex) { errors.Add($"Failed to un-flip type '{typeAlias}': {ex.Message}"); }
        }

        // 4. Recreate the original documents from the snapshot (with their original keys).
        int restoredCount = 0;

        foreach (SnapshotDoc doc in snapshot.Documents)
        {
            try
            {
                int parentIntId = doc.ParentKey == Guid.Empty
                    ? -1
                    : _entityService.GetId(doc.ParentKey, UmbracoObjectTypes.Document) is { Success: true } a
                        ? a.Result
                        : -1;

                IContent content = parentIntId == -1
                    ? _contentService.Create(doc.Name, -1, doc.ContentTypeAlias, Constants.Security.SuperUserId)
                    : _contentService.Create(doc.Name, doc.ParentKey, doc.ContentTypeAlias, Constants.Security.SuperUserId);

                // Preserve the original document key so references elsewhere resolve again after restore.
                // The originals were hard-deleted, and this content has no identity yet, so Save inserts with this key.
                content.Key = doc.Key;

                if (content.ContentType.VariesByCulture())
                {
                    if (doc.CultureNames is { Count: > 0 })
                    {
                        foreach ((string culture, string name) in doc.CultureNames)
                            content.SetCultureName(string.IsNullOrEmpty(name) ? doc.Name : name, culture);
                    }
                    else
                    {
                        var cultures = doc.Properties
                            .Where(p => !string.IsNullOrEmpty(p.Culture))
                            .Select(p => p.Culture!)
                            .Distinct()
                            .ToList();
                        if (cultures.Count == 0)
                        {
                            string? defaultIso = await _languageService.GetDefaultIsoCodeAsync();
                            if (!string.IsNullOrEmpty(defaultIso))
                                cultures.Add(defaultIso);
                        }
                        foreach (string culture in cultures)
                            content.SetCultureName(doc.Name, culture);
                    }
                }

                foreach (SnapshotProp prop in doc.Properties)
                {
                    object? value = DeserializePropertyValue(prop.Value);
                    if (value is not null)
                        content.SetValue(prop.Alias, value, prop.Culture, prop.Segment);
                }

                _contentService.Save(content, Constants.Security.SuperUserId);
                restoredCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to restore '{doc.Name}': {ex.Message}");
            }
        }

        // 5. Restore the property values that were rewritten across affected content
        //    (now that the original documents exist again and the editors are reverted).
        if (journal is not null)
            await RestorePropertyValuesAsync(journal, errors, performingUserKey);

        // 6. Restore the rewritten historical version rows.
        if (journal is not null)
            RestoreHistoricalRows(journal, errors);

        // 7. Delete the data types created by the migration (properties now repointed off them).
        if (journal is not null)
            await DeleteCreatedDataTypesAsync(journal, errors, performingUserKey);

        // 8. Delete the Library folders created by the migration.
        if (journal is not null)
            await DeleteLibraryFoldersAsync(journal, errors, performingUserKey);

        // 9. Clear persisted migration state.
        _keyValueService.SetValue($"Growcreate.LibraryMigrator.KeyMap.{containerKey}", "{}");
        _keyValueService.SetValue(ResultKey(containerKey), string.Empty);
        _keyValueService.SetValue(ReverseKey(containerKey), string.Empty);

        await _auditService.AddAsync(
            AuditType.Custom, performingUserKey, -1, "element-migration",
            $"Restored {restoredCount} document(s) from snapshot for container {containerKey}",
            string.Empty);

        if (journal is null)
            errors.Add("Note: this migration predates full rollback support — picker data type conversions, " +
                "remapped reference values and historical versions were not reverted and may need manual cleanup.");

        scope.Complete();

        result = new MigrationResultModel
        {
            Success = restoredCount > 0,
            ElementsCreated = restoredCount,
            Errors = errors,
        };
        }
        catch (Exception ex)
        {
            result = MigrationResultModel.Fail($"Restore failed and was rolled back: {ex.Message}");
        }

        try
        {
            scope.Dispose();
        }
        catch (Exception ex)
        {
            result = MigrationResultModel.Fail($"Restore commit failed and was rolled back: {ex.Message}");
        }

        return result;
    }

    private async Task RevertDataTypeConversionsAsync(
        ReverseJournal journal, List<string> errors, Guid userKey)
    {
        if (journal.DataTypeConversions.Count == 0) return;

        // Conversions can span document, media and member types, so revert per domain.
        foreach (ContentDomain domain in BuildContentDomains())
        {
            Dictionary<Guid, IContentTypeComposition> typesByKey = domain.Types.ToDictionary(t => t.Key);
            var revertedOwnerKeys = new HashSet<Guid>();

            foreach (IGrouping<Guid, DataTypeConversionEntry> grp in
                journal.DataTypeConversions.GroupBy(c => c.OwnerTypeKey))
            {
                if (!typesByKey.TryGetValue(grp.Key, out IContentTypeComposition? ct)) continue;

                bool changed = false;
                foreach (DataTypeConversionEntry entry in grp)
                {
                    IPropertyType? prop = ct.PropertyTypes.FirstOrDefault(p => p.Alias == entry.PropertyAlias);
                    if (prop is null) continue;
                    prop.DataTypeId = entry.OriginalDataTypeId;
                    prop.DataTypeKey = entry.OriginalDataTypeKey;
                    prop.PropertyEditorAlias = entry.OriginalEditorAlias;
                    changed = true;
                }

                if (!changed) continue;

                try
                {
                    await domain.UpdateTypeAsync(ct, userKey);
                    revertedOwnerKeys.Add(ct.Key);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to revert data types on '{ct.Alias}': {ex.Message}");
                }
            }

            // Refresh types that compose a reverted owner so inherited properties pick up the revert.
            if (revertedOwnerKeys.Count == 0) continue;

            foreach (IContentTypeComposition consumer in domain.Types)
            {
                if (revertedOwnerKeys.Contains(consumer.Key)) continue;
                if (!consumer.ContentTypeComposition.Any(c => revertedOwnerKeys.Contains(c.Key))) continue;

                try { await domain.UpdateTypeAsync(consumer, userKey); }
                catch (Exception ex)
                {
                    errors.Add($"Failed to refresh composing content type '{consumer.Alias}': {ex.Message}");
                }
            }
        }
    }

    private async Task RestorePropertyValuesAsync(
        ReverseJournal journal, List<string> errors, Guid userKey)
    {
        foreach (IGrouping<(Guid OwnerKey, string OwnerObjectType), PropertyValueEntry> grp in
            journal.PropertyValues.GroupBy(p => (p.OwnerKey, p.OwnerObjectType)))
        {
            (IContentBase? item, Func<Task>? save) = ResolveValueOwner(grp.Key.OwnerKey, grp.Key.OwnerObjectType, userKey);
            if (item is null || save is null)
            {
                errors.Add($"Owner {grp.Key.OwnerObjectType} {grp.Key.OwnerKey} not found — values not restored.");
                continue;
            }

            foreach (PropertyValueEntry entry in grp)
                item.SetValue(entry.Alias, entry.OriginalValue, entry.Culture, entry.Segment);

            try
            {
                await save();
            }
            catch (Exception ex)
            {
                errors.Add($"Value restore failed for '{item.Name}' ({item.Key}): {ex.Message}");
            }
        }
    }

    private (IContentBase? item, Func<Task>? save) ResolveValueOwner(Guid ownerKey, string objectType, Guid userKey)
    {
        switch (objectType)
        {
            case "document":
            {
                Attempt<int> id = _entityService.GetId(ownerKey, UmbracoObjectTypes.Document);
                if (!id.Success || _contentService.GetById(id.Result) is not IContent content) return (null, null);
                return (content, async () =>
                {
                    bool wasPublished = content.Published;
                    _contentService.Save(content, Constants.Security.SuperUserId);
                    if (wasPublished) await RepublishAsync(content, userKey);
                });
            }
            case "media":
            {
                Attempt<int> id = _entityService.GetId(ownerKey, UmbracoObjectTypes.Media);
                if (!id.Success || _mediaService.GetById(id.Result) is not IMedia media) return (null, null);
                return (media, () => { _mediaService.Save(media, Constants.Security.SuperUserId); return Task.CompletedTask; });
            }
            case "member":
            {
                Attempt<int> id = _entityService.GetId(ownerKey, UmbracoObjectTypes.Member);
                if (!id.Success || _memberService.GetById(id.Result) is not IMember member) return (null, null);
                return (member, () => { _memberService.Save(member, Constants.Security.SuperUserId); return Task.CompletedTask; });
            }
            default:
                return (null, null);
        }
    }

    private void RestoreHistoricalRows(ReverseJournal journal, List<string> errors)
    {
        if (journal.HistoricalRows.Count == 0) return;

        var db = _scopeAccessor.AmbientScope?.Database;
        if (db is null)
        {
            errors.Add("No ambient database scope available — historical version restore skipped.");
            return;
        }

        foreach (HistoricalRowEntry row in journal.HistoricalRows)
        {
            try
            {
                db.Execute("UPDATE umbracoPropertyData SET textValue = @0 WHERE id = @1",
                    row.OriginalTextValue, row.Id);
            }
            catch (Exception ex)
            {
                errors.Add($"Historical row {row.Id} restore failed: {ex.Message}");
            }
        }
    }

    private async Task DeleteCreatedDataTypesAsync(
        ReverseJournal journal, List<string> errors, Guid userKey)
    {
        if (journal.CreatedDataTypeKeys.Count == 0) return;

        var deleted = new HashSet<Guid>();
        foreach (Guid dtKey in journal.CreatedDataTypeKeys.Distinct())
        {
            try
            {
                if (await _dataTypeService.GetAsync(dtKey) is null) { deleted.Add(dtKey); continue; }
                await _dataTypeService.DeleteAsync(dtKey, userKey);
                deleted.Add(dtKey);
            }
            catch (Exception ex)
            {
                errors.Add($"Could not delete migration data type {dtKey}: {ex.Message}");
            }
        }

        // Drop persisted remap entries that point at the data types we just deleted,
        // so a future migration does not try to reuse a missing data type.
        if (deleted.Count > 0)
        {
            Dictionary<Guid, Guid> remap = LoadDataTypeRemap();
            var stale = remap.Where(kvp => deleted.Contains(kvp.Value)).Select(kvp => kvp.Key).ToList();
            if (stale.Count > 0)
            {
                foreach (Guid key in stale) remap.Remove(key);
                SaveDataTypeRemap(remap);
            }
        }
    }

    private async Task DeleteLibraryFoldersAsync(
        ReverseJournal journal, List<string> errors, Guid userKey)
    {
        foreach (Guid folderKey in journal.FolderKeys.Distinct())
        {
            try
            {
                await _elementContainerService.DeleteAsync(folderKey, userKey);
            }
            catch (Exception ex)
            {
                errors.Add($"Could not delete Library folder {folderKey}: {ex.Message}");
            }
        }
    }

    private static object? DeserializePropertyValue(string json)
    {
        var el = JsonSerializer.Deserialize<JsonElement>(json);
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.True => (object)true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Number when el.TryGetInt64(out long l) => l,
            JsonValueKind.Number => el.GetDouble(),
            _ => el.GetRawText(),
        };
    }

    private sealed record SnapshotRoot(DateTime TakenAt, List<SnapshotDoc> Documents);

    private sealed record SnapshotDoc(
        Guid Key,
        Guid ParentKey,
        string Name,
        string ContentTypeAlias,
        List<SnapshotProp> Properties,
        Dictionary<string, string>? CultureNames = null);

    private sealed record SnapshotProp(
        string Alias,
        string Value,
        string? Culture,
        string? Segment);

    private sealed record PersistedMigrationResult(int ElementsCreated, List<string> Errors);

    // --- Reverse journal: everything restore needs to undo the forward transforms ---

    // A point-in-time size of every journal list. Lets a rolled-back phase discard exactly the
    // entries it added (RollbackJournalTo) so the persisted journal matches what actually committed.
    private readonly record struct JournalCheckpoint(
        int Conversions, int Created, int Values, int Historical, int Folders);

    private static JournalCheckpoint CheckpointJournal(ReverseJournal j) =>
        new(j.DataTypeConversions.Count, j.CreatedDataTypeKeys.Count,
            j.PropertyValues.Count, j.HistoricalRows.Count, j.FolderKeys.Count);

    private static void RollbackJournalTo(ReverseJournal j, JournalCheckpoint cp)
    {
        TrimList(j.DataTypeConversions, cp.Conversions);
        TrimList(j.CreatedDataTypeKeys, cp.Created);
        TrimList(j.PropertyValues, cp.Values);
        TrimList(j.HistoricalRows, cp.Historical);
        TrimList(j.FolderKeys, cp.Folders);
    }

    private static void TrimList<T>(List<T> list, int count)
    {
        if (list.Count > count) list.RemoveRange(count, list.Count - count);
    }

    private sealed class ReverseJournal
    {
        // Content-type property repoints made by data-type conversion (to undo the editor switch).
        public List<DataTypeConversionEntry> DataTypeConversions { get; init; } = [];
        // Data types this migration created (to delete on restore; original data types are untouched).
        public List<Guid> CreatedDataTypeKeys { get; init; } = [];
        // Pre-migration property values that were rewritten (picker + block/RTE), to write back verbatim.
        public List<PropertyValueEntry> PropertyValues { get; init; } = [];
        // Historical umbracoPropertyData rows rewritten in place, keyed by row id.
        public List<HistoricalRowEntry> HistoricalRows { get; init; } = [];
        // Library folders created for the migration (to delete on restore).
        public List<Guid> FolderKeys { get; init; } = [];
    }

    private sealed record DataTypeConversionEntry(
        Guid OwnerTypeKey,
        string PropertyAlias,
        Guid OriginalDataTypeKey,
        int OriginalDataTypeId,
        string OriginalEditorAlias);

    private sealed record PropertyValueEntry(
        Guid OwnerKey,
        string OwnerObjectType,
        string Alias,
        string? Culture,
        string? Segment,
        string OriginalValue);

    private sealed record HistoricalRowEntry(int Id, string OriginalTextValue);

    // A content domain (documents / media / members): its content types, how to enumerate its
    // instances, how to persist an instance, and how to persist a content-type change.
    private sealed class ContentDomain
    {
        public required string ObjectType { get; init; }
        public required IReadOnlyList<IContentTypeComposition> Types { get; init; }
        public required Func<IContentTypeComposition, IReadOnlyList<IContentBase>> GetItems { get; init; }
        public required Func<IContentTypeComposition, Guid, Task> UpdateTypeAsync { get; init; }
        public required Func<IContentBase, Guid, Task> SaveAsync { get; init; }
    }
}
