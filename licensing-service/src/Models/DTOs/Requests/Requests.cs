using LicensingService.Models.Enums;

namespace LicensingService.Models.DTOs.Requests;

public record CreateLicenseRequest(
    string TenantId,
    int MaxApps,
    int MaxExecutionsPer24h,
    DateTime ValidFrom,
    DateTime ValidTo,
    LicenseStatus Status
);

public record RevokeLicenseRequest(string TenantId);

public record UpgradeLicenseRequest(
    string TenantId,
    int? MaxApps,
    int? MaxExecutionsPer24h,
    DateTime? ValidTo
);

public record RegisterAppRequest(
    string Name,
    string Description
);

public record StartJobRequest(
    string Name,
    Guid AppId
);

public record FinishJobRequest(Guid JobId);
