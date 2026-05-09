using LicensingService.Models.Enums;

namespace LicensingService.Models.Entities;

public class License
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public int MaxApps { get; set; }
    public int MaxExecutionsPer24h { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public LicenseStatus Status { get; set; } = LicenseStatus.ACTIVE;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<App> Apps { get; set; } = new List<App>();
}
