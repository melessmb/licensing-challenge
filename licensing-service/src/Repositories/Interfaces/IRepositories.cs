using LicensingService.Models.Entities;
using LicensingService.Models.Enums;

namespace LicensingService.Repositories.Interfaces;

public interface ILicenseRepository
{
    Task<License?> GetByIdAsync(Guid id);
    Task<License?> GetByTenantIdAsync(string tenantId);
    Task<IEnumerable<License>> GetAllAsync();
    Task<License> CreateAsync(License license);
    Task<License> UpdateAsync(License license);
    Task<bool> ExistsAsync(string tenantId);
}

public interface IAppRepository
{
    Task<App?> GetByIdAsync(Guid id);
    Task<IEnumerable<App>> GetByLicenseIdAsync(Guid licenseId);
    Task<int> CountByLicenseIdAsync(Guid licenseId);
    Task<App> CreateAsync(App app);
    Task<bool> BelongsToLicenseAsync(Guid appId, Guid licenseId);
}

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id);
    Task<IEnumerable<Job>> GetByLicenseIdAsync(Guid licenseId);
    Task<Job> CreateAsync(Job job);
    Task<Job> UpdateAsync(Job job);
    Task<bool> BelongsToLicenseAsync(Guid jobId, Guid licenseId);
}
