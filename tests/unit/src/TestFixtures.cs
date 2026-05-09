using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using LicensingService.Data;
using LicensingService.Models.Entities;
using LicensingService.Models.Enums;
using LicensingService.Services.Implementations;

namespace LicensingChallenge.Tests;

public static class TestFixtures
{
    // ── Database ───────────────────────────────────────────────────────────

    public static LicensingDbContext CreateInMemoryDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<LicensingDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new LicensingDbContext(options);
    }

    // ── Token Service ──────────────────────────────────────────────────────

    public static TokenService CreateTokenService(string? secret = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = secret
                    ?? "TestSecretKey2025!SuperSecureHMACKey256bitsForTesting"
            })
            .Build();
        return new TokenService(config);
    }

    // ── License factories ──────────────────────────────────────────────────

    public static License ActiveLicense(
        string tenantId      = "test-tenant",
        int    maxApps       = 3,
        int    maxExecutions = 10) => new()
    {
        Id                   = Guid.NewGuid(),
        TenantId             = tenantId,
        MaxApps              = maxApps,
        MaxExecutionsPer24h  = maxExecutions,
        ValidFrom            = DateTime.UtcNow.AddDays(-1),
        ValidTo              = DateTime.UtcNow.AddDays(30),
        Status               = LicenseStatus.ACTIVE
    };

    public static License ExpiredLicense(string tenantId = "expired-tenant") => new()
    {
        Id                   = Guid.NewGuid(),
        TenantId             = tenantId,
        MaxApps              = 5,
        MaxExecutionsPer24h  = 100,
        ValidFrom            = DateTime.UtcNow.AddDays(-60),
        ValidTo              = DateTime.UtcNow.AddDays(-1),
        Status               = LicenseStatus.ACTIVE
    };

    public static License RevokedLicense(string tenantId = "revoked-tenant") => new()
    {
        Id                   = Guid.NewGuid(),
        TenantId             = tenantId,
        MaxApps              = 5,
        MaxExecutionsPer24h  = 100,
        ValidFrom            = DateTime.UtcNow.AddDays(-10),
        ValidTo              = DateTime.UtcNow.AddDays(30),
        Status               = LicenseStatus.REVOKED
    };

    public static License SuspendedLicense(string tenantId = "suspended-tenant") => new()
    {
        Id                   = Guid.NewGuid(),
        TenantId             = tenantId,
        MaxApps              = 5,
        MaxExecutionsPer24h  = 100,
        ValidFrom            = DateTime.UtcNow.AddDays(-10),
        ValidTo              = DateTime.UtcNow.AddDays(30),
        Status               = LicenseStatus.SUSPENDED
    };

    // ── App & Job factories ────────────────────────────────────────────────

    public static App AppFor(License license, string name = "Test App") => new()
    {
        Id          = Guid.NewGuid(),
        Name        = name,
        Description = "Test description",
        LicenseId   = license.Id,
        License     = license
    };

    public static Job RunningJobFor(App app, string name = "Test Job") => new()
    {
        Id        = Guid.NewGuid(),
        Name      = name,
        AppId     = app.Id,
        App       = app,
        Status    = "RUNNING",
        StartedAt = DateTime.UtcNow
    };

    public static Job FinishedJobFor(App app, string name = "Finished Job") => new()
    {
        Id         = Guid.NewGuid(),
        Name       = name,
        AppId      = app.Id,
        App        = app,
        Status     = "FINISHED",
        StartedAt  = DateTime.UtcNow.AddMinutes(-10),
        FinishedAt = DateTime.UtcNow
    };
}
