using FluentAssertions;
using Moq;
using LicensingService.Models.DTOs.Requests;
using LicensingService.Models.Entities;
using LicensingService.Repositories.Interfaces;
using LicensingService.Services.Implementations;
using LicensingChallenge.Tests;
using Xunit;

namespace LicensingChallenge.Tests.Services;

public class JobServiceTests
{
    private readonly Mock<IJobRepository> _jobRepoMock;
    private readonly Mock<IAppRepository> _appRepoMock;
    private readonly JobService           _sut;

    public JobServiceTests()
    {
        _jobRepoMock = new Mock<IJobRepository>();
        _appRepoMock = new Mock<IAppRepository>();
        _sut         = new JobService(_jobRepoMock.Object, _appRepoMock.Object);
    }

    // ── StartAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_ValidApp_ReturnsJobResponse()
    {
        var license = TestFixtures.ActiveLicense();
        var app     = TestFixtures.AppFor(license);

        _appRepoMock.Setup(r => r.BelongsToLicenseAsync(app.Id, license.Id))
                    .ReturnsAsync(true);
        _jobRepoMock.Setup(r => r.CreateAsync(It.IsAny<Job>()))
                    .ReturnsAsync((Job j) => j);

        var result = await _sut.StartAsync(
            license.Id, new StartJobRequest("My Job", app.Id));

        result.Name.Should().Be("My Job");
        result.Status.Should().Be("RUNNING");
        result.AppId.Should().Be(app.Id);
    }

    [Fact]
    public async Task StartAsync_AppNotBelongToLicense_ThrowsKeyNotFoundException()
    {
        _appRepoMock.Setup(r => r.BelongsToLicenseAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                    .ReturnsAsync(false);

        var act = async () => await _sut.StartAsync(
            Guid.NewGuid(), new StartJobRequest("Job", Guid.NewGuid()));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*does not belong*");
    }

    [Fact]
    public async Task StartAsync_CallsJobRepositoryCreateOnce()
    {
        var license = TestFixtures.ActiveLicense();
        var app     = TestFixtures.AppFor(license);

        _appRepoMock.Setup(r => r.BelongsToLicenseAsync(app.Id, license.Id))
                    .ReturnsAsync(true);
        _jobRepoMock.Setup(r => r.CreateAsync(It.IsAny<Job>()))
                    .ReturnsAsync((Job j) => j);

        await _sut.StartAsync(license.Id, new StartJobRequest("Job", app.Id));

        _jobRepoMock.Verify(r => r.CreateAsync(It.IsAny<Job>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_CreatedJob_HasRunningStatus()
    {
        var license = TestFixtures.ActiveLicense();
        var app     = TestFixtures.AppFor(license);
        Job? capturedJob = null;

        _appRepoMock.Setup(r => r.BelongsToLicenseAsync(app.Id, license.Id))
                    .ReturnsAsync(true);
        _jobRepoMock.Setup(r => r.CreateAsync(It.IsAny<Job>()))
                    .Callback<Job>(j => capturedJob = j)
                    .ReturnsAsync((Job j) => j);

        await _sut.StartAsync(license.Id, new StartJobRequest("Job", app.Id));

        capturedJob.Should().NotBeNull();
        capturedJob!.Status.Should().Be("RUNNING");
        capturedJob.FinishedAt.Should().BeNull();
    }

    // ── FinishAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task FinishAsync_RunningJob_ReturnsFinishedJob()
    {
        var license    = TestFixtures.ActiveLicense();
        var app        = TestFixtures.AppFor(license);
        var runningJob = TestFixtures.RunningJobFor(app);

        _jobRepoMock.Setup(r => r.BelongsToLicenseAsync(runningJob.Id, license.Id))
                    .ReturnsAsync(true);
        _jobRepoMock.Setup(r => r.GetByIdAsync(runningJob.Id))
                    .ReturnsAsync(runningJob);
        _jobRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Job>()))
                    .ReturnsAsync((Job j) => j);

        var result = await _sut.FinishAsync(
            license.Id, new FinishJobRequest(runningJob.Id));

        result.Status.Should().Be("FINISHED");
        result.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FinishAsync_AlreadyFinishedJob_ThrowsInvalidOperationException()
    {
        var license     = TestFixtures.ActiveLicense();
        var app         = TestFixtures.AppFor(license);
        var finishedJob = TestFixtures.FinishedJobFor(app);

        _jobRepoMock.Setup(r => r.BelongsToLicenseAsync(finishedJob.Id, license.Id))
                    .ReturnsAsync(true);
        _jobRepoMock.Setup(r => r.GetByIdAsync(finishedJob.Id))
                    .ReturnsAsync(finishedJob);

        var act = async () => await _sut.FinishAsync(
            license.Id, new FinishJobRequest(finishedJob.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already finished*");
    }

    [Fact]
    public async Task FinishAsync_JobNotBelongToLicense_ThrowsKeyNotFoundException()
    {
        _jobRepoMock.Setup(r => r.BelongsToLicenseAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                    .ReturnsAsync(false);

        var act = async () => await _sut.FinishAsync(
            Guid.NewGuid(), new FinishJobRequest(Guid.NewGuid()));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task FinishAsync_CallsUpdateOnce()
    {
        var license    = TestFixtures.ActiveLicense();
        var app        = TestFixtures.AppFor(license);
        var runningJob = TestFixtures.RunningJobFor(app);

        _jobRepoMock.Setup(r => r.BelongsToLicenseAsync(runningJob.Id, license.Id))
                    .ReturnsAsync(true);
        _jobRepoMock.Setup(r => r.GetByIdAsync(runningJob.Id))
                    .ReturnsAsync(runningJob);
        _jobRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Job>()))
                    .ReturnsAsync((Job j) => j);

        await _sut.FinishAsync(license.Id, new FinishJobRequest(runningJob.Id));

        _jobRepoMock.Verify(r => r.UpdateAsync(
            It.Is<Job>(j => j.Status == "FINISHED")), Times.Once);
    }
}
