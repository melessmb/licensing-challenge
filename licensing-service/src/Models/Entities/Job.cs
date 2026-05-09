namespace LicensingService.Models.Entities;

public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid AppId { get; set; }
    public App App { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "RUNNING";
}
