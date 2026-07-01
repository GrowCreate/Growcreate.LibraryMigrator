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
public class MigrationController : ManagementApiControllerBase
{
    private readonly IElementMigrationService _migrationService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    public MigrationController(
        IElementMigrationService migrationService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _migrationService = migrationService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    [HttpGet("applicable/{documentKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Applicable(Guid documentKey)
    {
        if (!CurrentUserIsAdmin()) return Ok(new { applicable = false });
        bool applicable = await _migrationService.IsApplicableAsync(documentKey);
        return Ok(new { applicable });
    }

    [HttpGet("preview/{documentKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Preview(Guid documentKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();
        var report = await _migrationService.PreviewAsync(documentKey);
        return Ok(report);
    }

    [HttpPost("migrate/{documentKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Migrate(Guid documentKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();

        Guid? userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        if (userKey is null) return Unauthorized();

        var result = await _migrationService.MigrateAsync(documentKey, userKey.Value);
        return Ok(result);
    }

    [HttpGet("status/{documentKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Status(Guid documentKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();
        var status = await _migrationService.StatusAsync(documentKey);
        return Ok(status);
    }

    [HttpPost("restore/{documentKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Restore(Guid documentKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();

        Guid? userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        if (userKey is null) return Unauthorized();

        var result = await _migrationService.RestoreAsync(documentKey, userKey.Value);
        return Ok(result);
    }

    [HttpGet("types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Types()
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();
        var types = await _migrationService.GetEligibleTypesAsync();
        return Ok(types);
    }

    [HttpGet("types/{typeKey:guid}/preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PreviewType(Guid typeKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();
        var report = await _migrationService.PreviewTypeAsync(typeKey);
        return Ok(report);
    }

    [HttpPost("types/{typeKey:guid}/migrate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MigrateType(Guid typeKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();

        Guid? userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        if (userKey is null) return Unauthorized();

        var result = await _migrationService.MigrateTypeAsync(typeKey, userKey.Value);
        return Ok(result);
    }

    [HttpGet("types/{typeKey:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> StatusType(Guid typeKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();
        var status = await _migrationService.StatusAsync(typeKey);
        return Ok(status);
    }

    [HttpPost("types/{typeKey:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RestoreType(Guid typeKey)
    {
        if (!CurrentUserIsAdmin()) return Unauthorized();

        Guid? userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        if (userKey is null) return Unauthorized();

        var result = await _migrationService.RestoreAsync(typeKey, userKey.Value);
        return Ok(result);
    }

    private bool CurrentUserIsAdmin()
    {
        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        return user is not null && (user.IsAdmin() || user.IsSuper());
    }
}
