namespace MeteringService.Models.DTOs.Responses;

public record QuotaCheckResponse(
    bool Allowed,
    string TenantId,
    int Used,
    int Max,
    int Remaining
);

public record QuotaStatusResponse(
    string TenantId,
    int Used,
    int Max,
    int Remaining
);
