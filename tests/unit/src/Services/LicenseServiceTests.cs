using FluentAssertions;
using Moq;
using LicensingService.Models.DTOs.Requests;
using LicensingService.Models.Entities;
using LicensingService.Models.Enums;
using LicensingService.Repositories.Interfaces;
using LicensingService.Services.Implementations;
using LicensingChallenge.Tests;
using Xunit;

namespace LicensingChallenge.Tests.Services;

public class LicenseServiceTests
{
    private readonly Mock<ILicenseRepository> _repoMock;
    private readonly LicenseService           _sut;

    public LicenseServiceTests()
    {
        _repoMock = new Mock<ILicenseRepository>();
        var tokenService = TestFixtures.CreateTokenService();
        _sut = new LicenseService(_repoMock.Object, tokenService);
    }

    // ── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_NewTenant_ReturnsLicenseWithToken()
    {
        _repoMock.Setup(r => r.ExistsAsync("acme")).ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<License>()))
                 .ReturnsAsync((License l) => l);

        var request = new CreateLicenseRequest(
            "acme", 2, 100,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(30),
            LicenseStatus.ACTIVE);

        var result = await _sut.CreateAsync(request);

        result.TenantId.Should().Be("acme");
        result.MaxApps.Should().Be(2);
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAsync_ExistingTenant_ThrowsInvalidOperationException()
    {
        _repoMock.Setup(r => r.ExistsAsync("acme")).ReturnsAsync(true);

        var request = new CreateLicenseRequest(
            "acme", 2, 100,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(30),
            LicenseStatus.ACTIVE);

        var act = async () => await _sut.CreateAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_CallsRepositoryCreateOnce()
    {
        _repoMock.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<License>()))
                 .ReturnsAsync((License l) => l);

        var request = new CreateLicenseRequest(
            "new-tenant", 5, 50,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(30),
            LicenseStatus.ACTIVE);

        await _sut.CreateAsync(request);

        _repoMock.Verify(r => r.CreateAsync(It.IsAny<License>()), Times.Once);
    }

    // ── RevokeAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAsync_ExistingTenant_SetsStatusToRevoked()
    {
        var license = TestFixtures.ActiveLicense("revoke-me");
        _repoMock.Setup(r => r.GetByTenantIdAsync("revoke-me")).ReturnsAsync(license);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<License>()))
                 .ReturnsAsync((License l) => l);

        var result = await _sut.RevokeAsync("revoke-me");

        result.Should().NotBeNull();
        result!.Status.Should().Be(LicenseStatus.REVOKED);
    }

    [Fact]
    public async Task RevokeAsync_NonExistentTenant_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByTenantIdAsync(It.IsAny<string>()))
                 .ReturnsAsync((License?)null);

        var result = await _sut.RevokeAsync("ghost-tenant");
        result.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAsync_CallsUpdateRepository()
    {
        var license = TestFixtures.ActiveLicense("revoke-test");
        _repoMock.Setup(r => r.GetByTenantIdAsync("revoke-test")).ReturnsAsync(license);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<License>()))
                 .ReturnsAsync((License l) => l);

        await _sut.RevokeAsync("revoke-test");

        _repoMock.Verify(r => r.UpdateAsync(
            It.Is<License>(l => l.Status == LicenseStatus.REVOKED)), Times.Once);
    }

    // ── UpgradeAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpgradeAsync_ValidRequest_UpdatesMaxApps()
    {
        var license = TestFixtures.ActiveLicense("upgrade-me", maxApps: 2);
        _repoMock.Setup(r => r.GetByTenantIdAsync("upgrade-me")).ReturnsAsync(license);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<License>()))
                 .ReturnsAsync((License l) => l);

        var result = await _sut.UpgradeAsync(
            new UpgradeLicenseRequest("upgrade-me", MaxApps: 10, null, null));

        result!.MaxApps.Should().Be(10);
    }

    [Fact]
    public async Task UpgradeAsync_ValidRequest_UpdatesMaxExecutions()
    {
        var license = TestFixtures.ActiveLicense("upgrade-exec", maxExecutions: 50);
        _repoMock.Setup(r => r.GetByTenantIdAsync("upgrade-exec")).ReturnsAsync(license);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<License>()))
                 .ReturnsAsync((License l) => l);

        var result = await _sut.UpgradeAsync(
            new UpgradeLicenseRequest("upgrade-exec", null, MaxExecutionsPer24h: 500, null));

        result!.MaxExecutionsPer24h.Should().Be(500);
    }

    [Fact]
    public async Task UpgradeAsync_GeneratesNewToken()
    {
        var license = TestFixtures.ActiveLicense("token-upgrade");
        _repoMock.Setup(r => r.GetByTenantIdAsync("token-upgrade")).ReturnsAsync(license);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<License>()))
                 .ReturnsAsync((License l) => l);

        var result = await _sut.UpgradeAsync(
            new UpgradeLicenseRequest("token-upgrade", MaxApps: 20, null, null));

        result!.Token.Should().NotBeNullOrEmpty();
    }

    // ── ValidateTokenAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ValidateTokenAsync_ValidActiveLicense_ReturnsIsValidTrue()
    {
        var license = TestFixtures.ActiveLicense("validate-me");
        var token   = TestFixtures.CreateTokenService().GenerateToken(license);

        _repoMock.Setup(r => r.GetByTenantIdAsync("validate-me")).ReturnsAsync(license);

        var result = await _sut.ValidateTokenAsync(token);

        result.IsValid.Should().BeTrue();
        result.TenantId.Should().Be("validate-me");
        result.LicenseId.Should().Be(license.Id);
    }

    [Fact]
    public async Task ValidateTokenAsync_RevokedLicense_ReturnsIsValidFalse()
    {
        var license = TestFixtures.RevokedLicense("revoked-check");
        var token   = TestFixtures.CreateTokenService().GenerateToken(license);

        _repoMock.Setup(r => r.GetByTenantIdAsync("revoked-check")).ReturnsAsync(license);

        var result = await _sut.ValidateTokenAsync(token);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("REVOKED");
    }

    [Fact]
    public async Task ValidateTokenAsync_SuspendedLicense_ReturnsIsValidFalse()
    {
        var license = TestFixtures.SuspendedLicense("suspended-check");
        var token   = TestFixtures.CreateTokenService().GenerateToken(license);

        _repoMock.Setup(r => r.GetByTenantIdAsync("suspended-check")).ReturnsAsync(license);

        var result = await _sut.ValidateTokenAsync(token);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("SUSPENDED");
    }

    [Fact]
    public async Task ValidateTokenAsync_InvalidJwt_ReturnsIsValidFalse()
    {
        var result = await _sut.ValidateTokenAsync("bad.token.here");

        result.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateTokenAsync_LicenseNotInDb_ReturnsIsValidFalse()
    {
        var license = TestFixtures.ActiveLicense("ghost-tenant");
        var token   = TestFixtures.CreateTokenService().GenerateToken(license);

        _repoMock.Setup(r => r.GetByTenantIdAsync("ghost-tenant"))
                 .ReturnsAsync((License?)null);

        var result = await _sut.ValidateTokenAsync(token);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    // ── GetAllAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllLicenses()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
        {
            TestFixtures.ActiveLicense("t1"),
            TestFixtures.ActiveLicense("t2"),
            TestFixtures.ActiveLicense("t3")
        });

        var result = await _sut.GetAllAsync();
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetAllAsync())
                 .ReturnsAsync(Array.Empty<LicensingService.Models.Entities.License>());

        var result = await _sut.GetAllAsync();
        result.Should().BeEmpty();
    }
}
