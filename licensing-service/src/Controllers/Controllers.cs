using Microsoft.AspNetCore.Mvc;
using LicensingService.Models.DTOs.Requests;
using LicensingService.Services.Interfaces;

namespace LicensingService.Controllers;

[ApiController]
[Route("api/licenses")]
public class LicensesController : ControllerBase
{
    private readonly ILicenseService _service;
    public LicensesController(ILicenseService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLicenseRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return Created($"/api/licenses/{result.Id}", result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _service.GetAllAsync());

    [HttpGet("{tenantId}")]
    public async Task<IActionResult> GetByTenantId(string tenantId)
    {
        var result = await _service.GetByTenantIdAsync(tenantId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] string token)
    {
        var result = await _service.ValidateTokenAsync(token);
        return result.IsValid ? Ok(result) : StatusCode(403, result);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromBody] RevokeLicenseRequest request)
    {
        var result = await _service.RevokeAsync(request.TenantId);
        return result is null ? NotFound(new { error = "License not found" }) : Ok(result);
    }

    [HttpPost("upgrade")]
    public async Task<IActionResult> Upgrade([FromBody] UpgradeLicenseRequest request)
    {
        var result = await _service.UpgradeAsync(request);
        return result is null ? NotFound(new { error = "License not found" }) : Ok(result);
    }
}

[ApiController]
[Route("api/apps")]
public class AppsController : ControllerBase
{
    private readonly IAppService     _appService;
    private readonly ILicenseService _licenseService;

    public AppsController(IAppService appService, ILicenseService licenseService)
    {
        _appService     = appService;
        _licenseService = licenseService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromHeader(Name = "Authorization")] string authorization,
        [FromBody] RegisterAppRequest request)
    {
        var validation = await ValidateBearerAsync(authorization);
        if (!validation.IsValid)
            return StatusCode(403, new { error = validation.Error });

        try
        {
            var app = await _appService.RegisterAsync(validation.LicenseId!.Value, request);
            return Created($"/api/apps/{app.Id}", app);
        }
        catch (InvalidOperationException ex) { return StatusCode(429, new { error = ex.Message }); }
        catch (KeyNotFoundException ex)      { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromHeader(Name = "Authorization")] string authorization)
    {
        var validation = await ValidateBearerAsync(authorization);
        if (!validation.IsValid)
            return StatusCode(403, new { error = validation.Error });

        return Ok(await _appService.GetByLicenseIdAsync(validation.LicenseId!.Value));
    }

    private async Task<(bool IsValid, string? Error, Guid? LicenseId)> ValidateBearerAsync(string auth)
    {
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer "))
            return (false, "Missing Authorization header", null);

        var token  = auth["Bearer ".Length..].Trim();
        var result = await _licenseService.ValidateTokenAsync(token);
        return result.IsValid
            ? (true, null, result.LicenseId)
            : (false, result.Error, null);
    }
}

[ApiController]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly IJobService     _jobService;
    private readonly ILicenseService _licenseService;

    public JobsController(IJobService jobService, ILicenseService licenseService)
    {
        _jobService     = jobService;
        _licenseService = licenseService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(
        [FromHeader(Name = "Authorization")] string authorization,
        [FromBody] StartJobRequest request)
    {
        var validation = await ValidateBearerAsync(authorization);
        if (!validation.IsValid)
            return StatusCode(403, new { error = validation.Error });

        try
        {
            var job = await _jobService.StartAsync(validation.LicenseId!.Value, request);
            return Created($"/api/jobs/{job.Id}", job);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("finish")]
    public async Task<IActionResult> Finish(
        [FromHeader(Name = "Authorization")] string authorization,
        [FromBody] FinishJobRequest request)
    {
        var validation = await ValidateBearerAsync(authorization);
        if (!validation.IsValid)
            return StatusCode(403, new { error = validation.Error });

        try
        {
            var job = await _jobService.FinishAsync(validation.LicenseId!.Value, request);
            return Ok(job);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromHeader(Name = "Authorization")] string authorization)
    {
        var validation = await ValidateBearerAsync(authorization);
        if (!validation.IsValid)
            return StatusCode(403, new { error = validation.Error });

        return Ok(await _jobService.GetByLicenseIdAsync(validation.LicenseId!.Value));
    }

    private async Task<(bool IsValid, string? Error, Guid? LicenseId)> ValidateBearerAsync(string auth)
    {
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer "))
            return (false, "Missing Authorization header", null);

        var token  = auth["Bearer ".Length..].Trim();
        var result = await _licenseService.ValidateTokenAsync(token);
        return result.IsValid
            ? (true, null, result.LicenseId)
            : (false, result.Error, null);
    }
}
