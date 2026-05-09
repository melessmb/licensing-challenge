namespace LicensingService.Models.Entities;

public class App
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid LicenseId { get; set; }
    public License License { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
