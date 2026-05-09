using LicensingService.Models.Enums;

namespace LicensingService.Models.DTOs.Responses;

public record LicenseResponse(
    Guid Id,
    string TenantId,
    int MaxApps,
    int MaxExecutionsPer24h,
    DateTime ValidFrom,
    DateTime ValidTo,
    LicenseStatus Status,
    string Token
);

public record AppResponse(
    Guid Id,
    string Name,
    string Description,
    Guid LicenseId,
    DateTime CreatedAt
);

public record JobResponse(
    Guid Id,
    string Name,
    Guid AppId,
    string Status,
    DateTime StartedAt,
    DateTime? FinishedAt
);

public record LicenseValidationResponse(
    bool IsValid,
    string? Error,
    Guid? LicenseId,
    string? TenantId,
    int MaxApps,
    int MaxExecutionsPer24h,
    int CurrentAppCount
);
