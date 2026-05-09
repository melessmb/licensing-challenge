using Microsoft.EntityFrameworkCore;
using LicensingService.Data;
using LicensingService.Models.Entities;
using LicensingService.Repositories.Interfaces;

namespace LicensingService.Repositories.Implementations;

public class LicenseRepository : ILicenseRepository
{
    private readonly LicensingDbContext _db;
    public LicenseRepository(LicensingDbContext db) => _db = db;

    public async Task<License?> GetByIdAsync(Guid id) =>
        await _db.Licenses.Include(l => l.Apps).FirstOrDefaultAsync(l => l.Id == id);

    public async Task<License?> GetByTenantIdAsync(string tenantId) =>
        await _db.Licenses.Include(l => l.Apps).FirstOrDefaultAsync(l => l.TenantId == tenantId);

    public async Task<IEnumerable<License>> GetAllAsync() =>
        await _db.Licenses.ToListAsync();

    public async Task<License> CreateAsync(License license)
    {
        _db.Licenses.Add(license);
        await _db.SaveChangesAsync();
        return license;
    }

    public async Task<License> UpdateAsync(License license)
    {
        license.UpdatedAt = DateTime.UtcNow;
        _db.Licenses.Update(license);
        await _db.SaveChangesAsync();
        return license;
    }

    public async Task<bool> ExistsAsync(string tenantId) =>
        await _db.Licenses.AnyAsync(l => l.TenantId == tenantId);
}

public class AppRepository : IAppRepository
{
    private readonly LicensingDbContext _db;
    public AppRepository(LicensingDbContext db) => _db = db;

    public async Task<App?> GetByIdAsync(Guid id) =>
        await _db.Apps.Include(a => a.License).FirstOrDefaultAsync(a => a.Id == id);

    public async Task<IEnumerable<App>> GetByLicenseIdAsync(Guid licenseId) =>
        await _db.Apps.Where(a => a.LicenseId == licenseId).ToListAsync();

    public async Task<int> CountByLicenseIdAsync(Guid licenseId) =>
        await _db.Apps.CountAsync(a => a.LicenseId == licenseId);

    public async Task<App> CreateAsync(App app)
    {
        _db.Apps.Add(app);
        await _db.SaveChangesAsync();
        return app;
    }

    public async Task<bool> BelongsToLicenseAsync(Guid appId, Guid licenseId) =>
        await _db.Apps.AnyAsync(a => a.Id == appId && a.LicenseId == licenseId);
}

public class JobRepository : IJobRepository
{
    private readonly LicensingDbContext _db;
    public JobRepository(LicensingDbContext db) => _db = db;

    public async Task<Job?> GetByIdAsync(Guid id) =>
        await _db.Jobs.Include(j => j.App).FirstOrDefaultAsync(j => j.Id == id);

    public async Task<IEnumerable<Job>> GetByLicenseIdAsync(Guid licenseId) =>
        await _db.Jobs.Include(j => j.App)
            .Where(j => j.App.LicenseId == licenseId)
            .ToListAsync();

    public async Task<Job> CreateAsync(Job job)
    {
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();
        return job;
    }

    public async Task<Job> UpdateAsync(Job job)
    {
        _db.Jobs.Update(job);
        await _db.SaveChangesAsync();
        return job;
    }

    public async Task<bool> BelongsToLicenseAsync(Guid jobId, Guid licenseId) =>
        await _db.Jobs.Include(j => j.App)
            .AnyAsync(j => j.Id == jobId && j.App.LicenseId == licenseId);
}
