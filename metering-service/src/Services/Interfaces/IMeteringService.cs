namespace MeteringService.Services.Interfaces;

public interface IMeteringService
{
    Task<(bool allowed, int used, int remaining)> CheckAndIncrementAsync(
        string tenantId, int maxExecutions);

    Task<int> GetCurrentUsageAsync(string tenantId);

    Task DecrementAsync(string tenantId);

    Task ResetAsync(string tenantId);
}
