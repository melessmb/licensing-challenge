using LicensingService.Models.DTOs.Requests;
using LicensingService.Models.DTOs.Responses;
using LicensingService.Models.Entities;

namespace LicensingService.Services.Interfaces;

public interface ITokenService
{
    string GenerateToken(License license);
    (bool isValid, string? tenantId) ValidateToken(string token);
}

public interface ILicenseService
{
    Task<LicenseResponse> CreateAsync(CreateLicenseRequest request);
    Task<LicenseResponse?> GetByTenantIdAsync(string tenantId);
    Task<IEnumerable<LicenseResponse>> GetAllAsync();
    Task<LicenseResponse?> RevokeAsync(string tenantId);
    Task<LicenseResponse?> UpgradeAsync(UpgradeLicenseRequest request);
    Task<LicenseValidationResponse> ValidateTokenAsync(string token);
}

public interface IAppService
{
    Task<AppResponse> RegisterAsync(Guid licenseId, RegisterAppRequest request);
    Task<IEnumerable<AppResponse>> GetByLicenseIdAsync(Guid licenseId);
}

public interface IJobService
{
    Task<JobResponse> StartAsync(Guid licenseId, StartJobRequest request);
    Task<JobResponse> FinishAsync(Guid licenseId, FinishJobRequest request);
    Task<IEnumerable<JobResponse>> GetByLicenseIdAsync(Guid licenseId);
}
