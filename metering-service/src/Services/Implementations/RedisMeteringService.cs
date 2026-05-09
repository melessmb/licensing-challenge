using StackExchange.Redis;
using MeteringService.Services.Interfaces;

namespace MeteringService.Services.Implementations;

public class RedisMeteringService : IMeteringService
{
    private readonly IDatabase _redis;
    private const int WindowSeconds = 86400; // 24h

    private const string LuaScript = @"
        local key         = KEYS[1]
        local windowStart = tonumber(ARGV[1])
        local now         = tonumber(ARGV[2])
        local max         = tonumber(ARGV[3])
        local execId      = ARGV[4]

        -- Remove entries outside the 24h sliding window
        redis.call('ZREMRANGEBYSCORE', key, '-inf', windowStart)

        -- Count current valid executions
        local count = redis.call('ZCARD', key)

        -- Refuse if quota is reached
        if count >= max then
            return {0, count, 0}
        end

        -- Add new execution atomically
        redis.call('ZADD', key, now, execId)
        redis.call('EXPIRE', key, 86400)

        local newCount  = count + 1
        local remaining = max - newCount
        return {1, newCount, remaining}
    ";

    public RedisMeteringService(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task<(bool allowed, int used, int remaining)> CheckAndIncrementAsync(
        string tenantId, int maxExecutions)
    {
        var key         = BuildKey(tenantId);
        var now         = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - WindowSeconds;
        var execId      = Guid.NewGuid().ToString();

        var result = (RedisResult[])await _redis.ScriptEvaluateAsync(
            LuaScript,
            new RedisKey[]   { key },
            new RedisValue[] { windowStart, now, maxExecutions, execId }
        );

        var allowed   = (long)result[0] == 1;
        var used      = (int)(long)result[1];
        var remaining = (int)(long)result[2];

        return (allowed, used, remaining);
    }

    public async Task<int> GetCurrentUsageAsync(string tenantId)
    {
        var key         = BuildKey(tenantId);
        var windowStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - WindowSeconds;

        await _redis.SortedSetRemoveRangeByScoreAsync(
            key, double.NegativeInfinity, windowStart);

        return (int)await _redis.SortedSetLengthAsync(key);
    }

    public async Task DecrementAsync(string tenantId)
    {
        var key     = BuildKey(tenantId);
        var entries = await _redis.SortedSetRangeByScoreAsync(
            key, double.NegativeInfinity, double.PositiveInfinity,
            order: Order.Descending, take: 1);

        if (entries.Length > 0)
            await _redis.SortedSetRemoveAsync(key, entries[0]);
    }

    public async Task ResetAsync(string tenantId)
    {
        await _redis.KeyDeleteAsync(BuildKey(tenantId));
    }

    private static string BuildKey(string tenantId) =>
        $"metering:{tenantId}:executions";
}
