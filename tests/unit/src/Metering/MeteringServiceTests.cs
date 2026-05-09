using FluentAssertions;
using LicensingChallenge.Tests;
using Xunit;

namespace LicensingChallenge.Tests.Metering;

/// <summary>
/// Tests for the sliding window quota logic.
/// Uses InMemoryMeteringService to mirror Redis Sorted Set
/// behaviour without requiring a running Redis instance.
/// </summary>
public class MeteringServiceTests
{
    private readonly InMemoryMeteringService _sut;

    public MeteringServiceTests()
    {
        _sut = new InMemoryMeteringService();
    }

    // ── CheckAndIncrementAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CheckAndIncrement_FirstCall_IsAllowed()
    {
        var (allowed, used, remaining) = await _sut.CheckAndIncrementAsync("t1", 10);

        allowed.Should().BeTrue();
        used.Should().Be(1);
        remaining.Should().Be(9);
    }

    [Fact]
    public async Task CheckAndIncrement_WithinQuota_IsAllowed()
    {
        for (int i = 0; i < 5; i++)
            await _sut.CheckAndIncrementAsync("t2", 10);

        var (allowed, used, remaining) = await _sut.CheckAndIncrementAsync("t2", 10);

        allowed.Should().BeTrue();
        used.Should().Be(6);
        remaining.Should().Be(4);
    }

    [Fact]
    public async Task CheckAndIncrement_AtExactLimit_IsAllowed()
    {
        for (int i = 0; i < 9; i++)
            await _sut.CheckAndIncrementAsync("t3", 10);

        var (allowed, used, _) = await _sut.CheckAndIncrementAsync("t3", 10);

        allowed.Should().BeTrue();
        used.Should().Be(10);
    }

    [Fact]
    public async Task CheckAndIncrement_ExceedingQuota_IsRefused()
    {
        for (int i = 0; i < 10; i++)
            await _sut.CheckAndIncrementAsync("t4", 10);

        var (allowed, used, remaining) = await _sut.CheckAndIncrementAsync("t4", 10);

        allowed.Should().BeFalse();
        used.Should().Be(10);
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task CheckAndIncrement_QuotaOfOne_SecondCallRefused()
    {
        await _sut.CheckAndIncrementAsync("t5", 1);

        var (allowed, _, _) = await _sut.CheckAndIncrementAsync("t5", 1);

        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAndIncrement_DifferentTenants_AreIsolated()
    {
        for (int i = 0; i < 5; i++)
            await _sut.CheckAndIncrementAsync("tenant-full", 5);

        var (allowed, _, _) = await _sut.CheckAndIncrementAsync("tenant-free", 5);

        allowed.Should().BeTrue();
    }

    // ── GetCurrentUsageAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentUsage_AfterExecutions_ReturnsCorrectCount()
    {
        for (int i = 0; i < 7; i++)
            await _sut.CheckAndIncrementAsync("t6", 100);

        var usage = await _sut.GetCurrentUsageAsync("t6");
        usage.Should().Be(7);
    }

    [Fact]
    public async Task GetCurrentUsage_NewTenant_ReturnsZero()
    {
        var usage = await _sut.GetCurrentUsageAsync("brand-new");
        usage.Should().Be(0);
    }

    // ── DecrementAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Decrement_AfterIncrement_ReducesCount()
    {
        await _sut.CheckAndIncrementAsync("t7", 10);
        await _sut.CheckAndIncrementAsync("t7", 10);

        await _sut.DecrementAsync("t7");

        var usage = await _sut.GetCurrentUsageAsync("t7");
        usage.Should().Be(1);
    }

    // ── ResetAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Reset_AfterExecutions_ResetsToZero()
    {
        for (int i = 0; i < 5; i++)
            await _sut.CheckAndIncrementAsync("t8", 100);

        await _sut.ResetAsync("t8");

        var usage = await _sut.GetCurrentUsageAsync("t8");
        usage.Should().Be(0);
    }

    [Fact]
    public async Task Reset_AllowsNewExecutionsAfterReset()
    {
        for (int i = 0; i < 5; i++)
            await _sut.CheckAndIncrementAsync("t9", 5);

        await _sut.ResetAsync("t9");

        var (allowed, _, _) = await _sut.CheckAndIncrementAsync("t9", 5);
        allowed.Should().BeTrue();
    }

    // ── Sliding window ─────────────────────────────────────────────────────

    [Fact]
    public async Task SlidingWindow_ExpiredEntries_AreNotCounted()
    {
        var sut = new InMemoryMeteringService();

        // Inject 5 expired executions (older than 24h)
        for (int i = 0; i < 5; i++)
            sut.InjectExpiredExecution("t10");

        // Quota = 5, but expired ones should not count
        var (allowed, used, _) = await sut.CheckAndIncrementAsync("t10", maxExecutions: 5);

        allowed.Should().BeTrue();
        used.Should().Be(1);
    }
}

/// <summary>
/// In-memory implementation of IMeteringService for unit testing.
/// Mirrors the Redis Sorted Set sliding window logic exactly.
/// </summary>
public class InMemoryMeteringService : MeteringService.Services.Interfaces.IMeteringService
{
    private readonly Dictionary<string, List<DateTimeOffset>> _store = new();
    private const int WindowSeconds = 86400;

    public void InjectExpiredExecution(string tenantId)
    {
        if (!_store.ContainsKey(tenantId))
            _store[tenantId] = new List<DateTimeOffset>();

        _store[tenantId].Add(DateTimeOffset.UtcNow.AddSeconds(-WindowSeconds - 60));
    }

    public Task<(bool allowed, int used, int remaining)> CheckAndIncrementAsync(
        string tenantId, int maxExecutions)
    {
        if (!_store.ContainsKey(tenantId))
            _store[tenantId] = new List<DateTimeOffset>();

        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-WindowSeconds);
        _store[tenantId].RemoveAll(t => t < cutoff);

        var current = _store[tenantId].Count;

        if (current >= maxExecutions)
            return Task.FromResult((false, current, 0));

        _store[tenantId].Add(DateTimeOffset.UtcNow);
        var newCount  = current + 1;
        return Task.FromResult((true, newCount, maxExecutions - newCount));
    }

    public Task<int> GetCurrentUsageAsync(string tenantId)
    {
        if (!_store.TryGetValue(tenantId, out var list))
            return Task.FromResult(0);

        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-WindowSeconds);
        return Task.FromResult(list.Count(t => t >= cutoff));
    }

    public Task DecrementAsync(string tenantId)
    {
        if (_store.TryGetValue(tenantId, out var list) && list.Count > 0)
            list.RemoveAt(list.Count - 1);
        return Task.CompletedTask;
    }

    public Task ResetAsync(string tenantId)
    {
        _store.Remove(tenantId);
        return Task.CompletedTask;
    }
}
