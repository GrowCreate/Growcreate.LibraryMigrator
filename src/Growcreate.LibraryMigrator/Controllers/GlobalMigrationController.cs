using Asp.Versioning;
using Growcreate.LibraryMigrator.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace Growcreate.LibraryMigrator.Controllers;

[ApiController]
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("growcreate-library-migrator")]
[ApiExplorerSettings(GroupName = "Growcreate Library Migrator")]
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
public class GlobalMigrationController : ManagementApiControllerBase
{
    private readonly IGlobalMigrationService _globalMigrationService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    public GlobalMigrationController(
        IGlobalMigrationService globalMigrationService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _globalMigrationService = globalMigrationService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    [HttpGet("eligible-types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EligibleTypes()
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();
        var types = await _globalMigrationService.ListEligibleDocTypesAsync();
        return Ok(types);
    }

    [HttpGet("preview-type/{docTypeKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PreviewType(Guid docTypeKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();
        var report = await _globalMigrationService.PreviewByDocTypeAsync(docTypeKey);
        return Ok(report);
    }

    [HttpPost("migrate-type/{docTypeKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MigrateType(Guid docTypeKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();

        Guid? userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        if (userKey is null) return Unauthorized();

        var run = await _globalMigrationService.MigrateByDocTypeAsync(docTypeKey, userKey.Value);
        return Ok(run);
    }

    [HttpGet("runs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Runs()
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();
        var runs = await _globalMigrationService.ListRunsAsync();
        return Ok(runs);
    }

    [HttpPost("restore-run/{runId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RestoreRun(Guid runId)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();

        Guid? userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        if (userKey is null) return Unauthorized();

        var result = await _globalMigrationService.RestoreRunAsync(runId, userKey.Value);
        return Ok(result);
    }

    private bool CurrentUserIsAdmin()
    {
        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        return user is not null && (user.IsAdmin() || user.IsSuper());
    }
}
