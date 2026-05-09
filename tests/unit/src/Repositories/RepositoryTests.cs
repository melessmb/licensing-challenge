using FluentAssertions;
using LicensingService.Models.Entities;
using LicensingService.Models.Enums;
using LicensingService.Repositories.Implementations;
using LicensingChallenge.Tests;
using Xunit;

namespace LicensingChallenge.Tests.Repositories;

public class LicenseRepositoryTests
{
    // ── GetByTenantIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetByTenantIdAsync_ExistingTenant_ReturnsLicense()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var license = TestFixtures.ActiveLicense("get-tenant");
        db.Licenses.Add(license);
        await db.SaveChangesAsync();

        var repo   = new LicenseRepository(db);
        var result = await repo.GetByTenantIdAsync("get-tenant");

        result.Should().NotBeNull();
        result!.TenantId.Should().Be("get-tenant");
    }

    [Fact]
    public async Task GetByTenantIdAsync_NonExistentTenant_ReturnsNull()
    {
        var db   = TestFixtures.CreateInMemoryDb();
        var repo = new LicenseRepository(db);

        var result = await repo.GetByTenantIdAsync("ghost");
        result.Should().BeNull();
    }

    // ── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidLicense_PersistsToDatabase()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var repo    = new LicenseRepository(db);
        var license = TestFixtures.ActiveLicense("create-test");

        await repo.CreateAsync(license);

        db.Licenses.Should().HaveCount(1);
        db.Licenses.First().TenantId.Should().Be("create-test");
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreatedLicense()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var repo    = new LicenseRepository(db);
        var license = TestFixtures.ActiveLicense("return-test");

        var result = await repo.CreateAsync(license);
        result.TenantId.Should().Be("return-test");
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_RevokedLicense_PersistsStatusChange()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var license = TestFixtures.ActiveLicense("update-test");
        db.Licenses.Add(license);
        await db.SaveChangesAsync();

        var repo = new LicenseRepository(db);
        license.Status = LicenseStatus.REVOKED;
        await repo.UpdateAsync(license);

        var updated = await repo.GetByTenantIdAsync("update-test");
        updated!.Status.Should().Be(LicenseStatus.REVOKED);
    }

    // ── ExistsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_ExistingTenant_ReturnsTrue()
    {
        var db = TestFixtures.CreateInMemoryDb();
        db.Licenses.Add(TestFixtures.ActiveLicense("exists-tenant"));
        await db.SaveChangesAsync();

        var repo   = new LicenseRepository(db);
        var result = await repo.ExistsAsync("exists-tenant");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistentTenant_ReturnsFalse()
    {
        var db     = TestFixtures.CreateInMemoryDb();
        var repo   = new LicenseRepository(db);
        var result = await repo.ExistsAsync("not-there");
        result.Should().BeFalse();
    }

    // ── GetAllAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_MultiipleLicenses_ReturnsAll()
    {
        var db = TestFixtures.CreateInMemoryDb();
        db.Licenses.AddRange(
            TestFixtures.ActiveLicense("t1"),
            TestFixtures.ActiveLicense("t2"),
            TestFixtures.ActiveLicense("t3"));
        await db.SaveChangesAsync();

        var repo   = new LicenseRepository(db);
        var result = await repo.GetAllAsync();
        result.Should().HaveCount(3);
    }
}

public class AppRepositoryTests
{
    // ── CountByLicenseIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task CountByLicenseIdAsync_ReturnsCorrectCount()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var license = TestFixtures.ActiveLicense();
        db.Licenses.Add(license);
        db.Apps.AddRange(
            TestFixtures.AppFor(license, "App 1"),
            TestFixtures.AppFor(license, "App 2"));
        await db.SaveChangesAsync();

        var repo   = new AppRepository(db);
        var result = await repo.CountByLicenseIdAsync(license.Id);
        result.Should().Be(2);
    }

    [Fact]
    public async Task CountByLicenseIdAsync_NoApps_ReturnsZero()
    {
        var db   = TestFixtures.CreateInMemoryDb();
        var repo = new AppRepository(db);

        var result = await repo.CountByLicenseIdAsync(Guid.NewGuid());
        result.Should().Be(0);
    }

    // ── BelongsToLicenseAsync ──────────────────────────────────────────────

    [Fact]
    public async Task BelongsToLicenseAsync_AppBelongsToLicense_ReturnsTrue()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var license = TestFixtures.ActiveLicense();
        var app     = TestFixtures.AppFor(license);
        db.Licenses.Add(license);
        db.Apps.Add(app);
        await db.SaveChangesAsync();

        var repo   = new AppRepository(db);
        var result = await repo.BelongsToLicenseAsync(app.Id, license.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task BelongsToLicenseAsync_AppBelongsToDifferentLicense_ReturnsFalse()
    {
        var db       = TestFixtures.CreateInMemoryDb();
        var license1 = TestFixtures.ActiveLicense("l1");
        var license2 = TestFixtures.ActiveLicense("l2");
        var app      = TestFixtures.AppFor(license1);
        db.Licenses.AddRange(license1, license2);
        db.Apps.Add(app);
        await db.SaveChangesAsync();

        var repo   = new AppRepository(db);
        var result = await repo.BelongsToLicenseAsync(app.Id, license2.Id);
        result.Should().BeFalse();
    }

    // ── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidApp_PersistsToDatabase()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var license = TestFixtures.ActiveLicense();
        db.Licenses.Add(license);
        await db.SaveChangesAsync();

        var repo = new AppRepository(db);
        var app  = TestFixtures.AppFor(license, "Persisted App");
        await repo.CreateAsync(app);

        db.Apps.Should().HaveCount(1);
        db.Apps.First().Name.Should().Be("Persisted App");
    }
}

public class JobRepositoryTests
{
    // ── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidJob_PersistsToDatabase()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var license = TestFixtures.ActiveLicense();
        var app     = TestFixtures.AppFor(license);
        db.Licenses.Add(license);
        db.Apps.Add(app);
        await db.SaveChangesAsync();

        var repo = new JobRepository(db);
        var job  = TestFixtures.RunningJobFor(app, "My Job");
        await repo.CreateAsync(job);

        db.Jobs.Should().HaveCount(1);
        db.Jobs.First().Status.Should().Be("RUNNING");
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_FinishedJob_PersistsStatusChange()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var license = TestFixtures.ActiveLicense();
        var app     = TestFixtures.AppFor(license);
        var job     = TestFixtures.RunningJobFor(app);
        db.Licenses.Add(license);
        db.Apps.Add(app);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var repo = new JobRepository(db);
        job.Status     = "FINISHED";
        job.FinishedAt = DateTime.UtcNow;
        await repo.UpdateAsync(job);

        var updated = await repo.GetByIdAsync(job.Id);
        updated!.Status.Should().Be("FINISHED");
        updated.FinishedAt.Should().NotBeNull();
    }

    // ── BelongsToLicenseAsync ──────────────────────────────────────────────

    [Fact]
    public async Task BelongsToLicenseAsync_JobBelongsToLicense_ReturnsTrue()
    {
        var db      = TestFixtures.CreateInMemoryDb();
        var license = TestFixtures.ActiveLicense();
        var app     = TestFixtures.AppFor(license);
        var job     = TestFixtures.RunningJobFor(app);
        db.Licenses.Add(license);
        db.Apps.Add(app);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var repo   = new JobRepository(db);
        var result = await repo.BelongsToLicenseAsync(job.Id, license.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task BelongsToLicenseAsync_JobOfDifferentLicense_ReturnsFalse()
    {
        var db       = TestFixtures.CreateInMemoryDb();
        var license1 = TestFixtures.ActiveLicense("l1");
        var license2 = TestFixtures.ActiveLicense("l2");
        var app      = TestFixtures.AppFor(license1);
        var job      = TestFixtures.RunningJobFor(app);
        db.Licenses.AddRange(license1, license2);
        db.Apps.Add(app);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var repo   = new JobRepository(db);
        var result = await repo.BelongsToLicenseAsync(job.Id, license2.Id);
        result.Should().BeFalse();
    }
}
