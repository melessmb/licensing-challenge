using FluentAssertions;
using Moq;
using LicensingService.Models.DTOs.Requests;
using LicensingService.Models.Entities;
using LicensingService.Repositories.Interfaces;
using LicensingService.Services.Implementations;
using LicensingChallenge.Tests;
using Xunit;

namespace LicensingChallenge.Tests.Services;

public class AppServiceTests
{
    private readonly Mock<IAppRepository>     _appRepoMock;
    private readonly Mock<ILicenseRepository> _licenseRepoMock;
    private readonly AppService               _sut;

    public AppServiceTests()
    {
        _appRepoMock     = new Mock<IAppRepository>();
        _licenseRepoMock = new Mock<ILicenseRepository>();
        _sut             = new AppService(_appRepoMock.Object, _licenseRepoMock.Object);
    }

    // ── RegisterAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_WithinLimit_ReturnsAppResponse()
    {
        var license = TestFixtures.ActiveLicense(maxApps: 3);
        _licenseRepoMock.Setup(r => r.GetByIdAsync(license.Id)).ReturnsAsync(license);
        _appRepoMock.Setup(r => r.CountByLicenseIdAsync(license.Id)).ReturnsAsync(1);
        _appRepoMock.Setup(r => r.CreateAsync(It.IsAny<App>()))
                    .ReturnsAsync((App a) => a);

        var result = await _sut.RegisterAsync(
            license.Id, new RegisterAppRequest("My App", "Desc"));

        result.Name.Should().Be("My App");
        result.LicenseId.Should().Be(license.Id);
    }

    [Fact]
    public async Task RegisterAsync_AtExactLimit_Succeeds()
    {
        var license = TestFixtures.ActiveLicense(maxApps: 3);
        _licenseRepoMock.Setup(r => r.GetByIdAsync(license.Id)).ReturnsAsync(license);
        _appRepoMock.Setup(r => r.CountByLicenseIdAsync(license.Id)).ReturnsAsync(2);
        _appRepoMock.Setup(r => r.CreateAsync(It.IsAny<App>()))
                    .ReturnsAsync((App a) => a);

        var result = await _sut.RegisterAsync(
            license.Id, new RegisterAppRequest("Third App", "Last slot"));

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterAsync_MaxAppsReached_ThrowsInvalidOperationException()
    {
        var license = TestFixtures.ActiveLicense(maxApps: 2);
        _licenseRepoMock.Setup(r => r.GetByIdAsync(license.Id)).ReturnsAsync(license);
        _appRepoMock.Setup(r => r.CountByLicenseIdAsync(license.Id)).ReturnsAsync(2);

        var act = async () => await _sut.RegisterAsync(
            license.Id, new RegisterAppRequest("Overflow App", "Desc"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*App limit reached*");
    }

    [Fact]
    public async Task RegisterAsync_LicenseNotFound_ThrowsKeyNotFoundException()
    {
        _licenseRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                        .ReturnsAsync((License?)null);

        var act = async () => await _sut.RegisterAsync(
            Guid.NewGuid(), new RegisterAppRequest("App", "Desc"));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RegisterAsync_CallsRepositoryCreateOnce()
    {
        var license = TestFixtures.ActiveLicense(maxApps: 5);
        _licenseRepoMock.Setup(r => r.GetByIdAsync(license.Id)).ReturnsAsync(license);
        _appRepoMock.Setup(r => r.CountByLicenseIdAsync(license.Id)).ReturnsAsync(0);
        _appRepoMock.Setup(r => r.CreateAsync(It.IsAny<App>()))
                    .ReturnsAsync((App a) => a);

        await _sut.RegisterAsync(license.Id, new RegisterAppRequest("App", "Desc"));

        _appRepoMock.Verify(r => r.CreateAsync(It.IsAny<App>()), Times.Once);
    }

    // ── GetByLicenseIdAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetByLicenseIdAsync_ReturnsOnlyAppsForThatLicense()
    {
        var license = TestFixtures.ActiveLicense();
        var apps    = new[]
        {
            TestFixtures.AppFor(license, "App A"),
            TestFixtures.AppFor(license, "App B")
        };

        _appRepoMock.Setup(r => r.GetByLicenseIdAsync(license.Id))
                    .ReturnsAsync(apps);

        var result = await _sut.GetByLicenseIdAsync(license.Id);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(a => a.LicenseId.Should().Be(license.Id));
    }

    [Fact]
    public async Task GetByLicenseIdAsync_NoApps_ReturnsEmptyList()
    {
        _appRepoMock.Setup(r => r.GetByLicenseIdAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(Array.Empty<App>());

        var result = await _sut.GetByLicenseIdAsync(Guid.NewGuid());
        result.Should().BeEmpty();
    }
}
