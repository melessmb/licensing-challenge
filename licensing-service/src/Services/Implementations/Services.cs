using LicensingService.Models.DTOs.Requests;
using LicensingService.Models.DTOs.Responses;
using LicensingService.Models.Entities;
using LicensingService.Models.Enums;
using LicensingService.Repositories.Interfaces;
using LicensingService.Services.Interfaces;

namespace LicensingService.Services.Implementations;

// ── LicenseService ────────────────────────────────────────────────────────
public class LicenseService : ILicenseService
{
    private readonly ILicenseRepository _repo;
    private readonly ITokenService      _tokenService;

    public LicenseService(ILicenseRepository repo, ITokenService tokenService)
    {
        _repo         = repo;
        _tokenService = tokenService;
    }

    public async Task<LicenseResponse> CreateAsync(CreateLicenseRequest request)
    {
        if (await _repo.ExistsAsync(request.TenantId))
            throw new InvalidOperationException($"License for tenant '{request.TenantId}' already exists");

        var license = new License
        {
            TenantId             = request.TenantId,
            MaxApps              = request.MaxApps,
            MaxExecutionsPer24h  = request.MaxExecutionsPer24h,
            ValidFrom            = request.ValidFrom,
            ValidTo              = request.ValidTo,
            Status               = request.Status
        };

        var created = await _repo.CreateAsync(license);
        return ToResponse(created, _tokenService.GenerateToken(created));
    }

    public async Task<LicenseResponse?> GetByTenantIdAsync(string tenantId)
    {
        var license = await _repo.GetByTenantIdAsync(tenantId);
        return license is null ? null : ToResponse(license, _tokenService.GenerateToken(license));
    }

    public async Task<IEnumerable<LicenseResponse>> GetAllAsync()
    {
        var licenses = await _repo.GetAllAsync();
        return licenses.Select(l => ToResponse(l, ""));
    }

    public async Task<LicenseResponse?> RevokeAsync(string tenantId)
    {
        var license = await _repo.GetByTenantIdAsync(tenantId);
        if (license is null) return null;

        license.Status = LicenseStatus.REVOKED;
        var updated = await _repo.UpdateAsync(license);
        return ToResponse(updated, "");
    }

    public async Task<LicenseResponse?> UpgradeAsync(UpgradeLicenseRequest request)
    {
        var license = await _repo.GetByTenantIdAsync(request.TenantId);
        if (license is null) return null;

        if (request.MaxApps.HasValue)              license.MaxApps             = request.MaxApps.Value;
        if (request.MaxExecutionsPer24h.HasValue)  license.MaxExecutionsPer24h = request.MaxExecutionsPer24h.Value;
        if (request.ValidTo.HasValue)              license.ValidTo             = request.ValidTo.Value;

        var updated = await _repo.UpdateAsync(license);
        return ToResponse(updated, _tokenService.GenerateToken(updated));
    }

    public async Task<LicenseValidationResponse> ValidateTokenAsync(string token)
    {
        var (isValid, tenantId) = _tokenService.ValidateToken(token);

        if (!isValid || tenantId is null)
            return new LicenseValidationResponse(false, "Invalid or expired token", null, null, 0, 0, 0);

        var license = await _repo.GetByTenantIdAsync(tenantId);
        if (license is null)
            return new LicenseValidationResponse(false, "License not found", null, null, 0, 0, 0);

        if (license.Status != LicenseStatus.ACTIVE)
            return new LicenseValidationResponse(false, $"License is {license.Status}", null, null, 0, 0, 0);

        if (DateTime.UtcNow < license.ValidFrom || DateTime.UtcNow > license.ValidTo)
            return new LicenseValidationResponse(false, "License is outside valid period", null, null, 0, 0, 0);

        return new LicenseValidationResponse(
            true, null,
            license.Id, license.TenantId,
            license.MaxApps, license.MaxExecutionsPer24h,
            license.Apps.Count
        );
    }

    private static LicenseResponse ToResponse(License l, string token) => new(
        l.Id, l.TenantId, l.MaxApps, l.MaxExecutionsPer24h,
        l.ValidFrom, l.ValidTo, l.Status, token
    );
}

// ── AppService ────────────────────────────────────────────────────────────
public class AppService : IAppService
{
    private readonly IAppRepository     _appRepo;
    private readonly ILicenseRepository _licenseRepo;

    public AppService(IAppRepository appRepo, ILicenseRepository licenseRepo)
    {
        _appRepo     = appRepo;
        _licenseRepo = licenseRepo;
    }

    public async Task<AppResponse> RegisterAsync(Guid licenseId, RegisterAppRequest request)
    {
        var license = await _licenseRepo.GetByIdAsync(licenseId)
            ?? throw new KeyNotFoundException("License not found");

        var count = await _appRepo.CountByLicenseIdAsync(licenseId);
        if (count >= license.MaxApps)
            throw new InvalidOperationException(
                $"App limit reached. Max: {license.MaxApps}, Current: {count}");

        var app = new App
        {
            Name        = request.Name,
            Description = request.Description,
            LicenseId   = licenseId
        };

        var created = await _appRepo.CreateAsync(app);
        return ToResponse(created);
    }

    public async Task<IEnumerable<AppResponse>> GetByLicenseIdAsync(Guid licenseId)
    {
        var apps = await _appRepo.GetByLicenseIdAsync(licenseId);
        return apps.Select(ToResponse);
    }

    private static AppResponse ToResponse(App a) =>
        new(a.Id, a.Name, a.Description, a.LicenseId, a.CreatedAt);
}

// ── JobService ────────────────────────────────────────────────────────────
public class JobService : IJobService
{
    private readonly IJobRepository _jobRepo;
    private readonly IAppRepository _appRepo;

    public JobService(IJobRepository jobRepo, IAppRepository appRepo)
    {
        _jobRepo = jobRepo;
        _appRepo = appRepo;
    }

    public async Task<JobResponse> StartAsync(Guid licenseId, StartJobRequest request)
    {
        var appBelongs = await _appRepo.BelongsToLicenseAsync(request.AppId, licenseId);
        if (!appBelongs)
            throw new KeyNotFoundException("App not found or does not belong to this license");

        var job = new Job
        {
            Name      = request.Name,
            AppId     = request.AppId,
            StartedAt = DateTime.UtcNow,
            Status    = "RUNNING"
        };

        var created = await _jobRepo.CreateAsync(job);
        return ToResponse(created);
    }

    public async Task<JobResponse> FinishAsync(Guid licenseId, FinishJobRequest request)
    {
        var belongs = await _jobRepo.BelongsToLicenseAsync(request.JobId, licenseId);
        if (!belongs)
            throw new KeyNotFoundException("Job not found or does not belong to this license");

        var job = await _jobRepo.GetByIdAsync(request.JobId)
            ?? throw new KeyNotFoundException("Job not found");

        if (job.Status == "FINISHED")
            throw new InvalidOperationException("Job is already finished");

        job.FinishedAt = DateTime.UtcNow;
        job.Status     = "FINISHED";

        var updated = await _jobRepo.UpdateAsync(job);
        return ToResponse(updated);
    }

    public async Task<IEnumerable<JobResponse>> GetByLicenseIdAsync(Guid licenseId)
    {
        var jobs = await _jobRepo.GetByLicenseIdAsync(licenseId);
        return jobs.Select(ToResponse);
    }

    private static JobResponse ToResponse(Job j) =>
        new(j.Id, j.Name, j.AppId, j.Status, j.StartedAt, j.FinishedAt);
}
