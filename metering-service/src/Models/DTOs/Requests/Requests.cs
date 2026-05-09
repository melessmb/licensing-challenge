namespace MeteringService.Models.DTOs.Requests;

public record CheckQuotaRequest(string TenantId, int MaxExecutionsPer24h);
public record DecrementQuotaRequest(string TenantId);
