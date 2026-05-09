using Microsoft.EntityFrameworkCore;
using LicensingService.Data;
using LicensingService.Repositories.Interfaces;
using LicensingService.Repositories.Implementations;
using LicensingService.Services.Interfaces;
using LicensingService.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──
builder.Services.AddDbContext<LicensingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Repositories (D of SOLID — depend on abstractions) ──
builder.Services.AddScoped<ILicenseRepository, LicenseRepository>();
builder.Services.AddScoped<IAppRepository,     AppRepository>();
builder.Services.AddScoped<IJobRepository,     JobRepository>();

// ── Services ──
builder.Services.AddSingleton<ITokenService,  TokenService>();
builder.Services.AddScoped<ILicenseService,   LicenseService>();
builder.Services.AddScoped<IAppService,       AppService>();
builder.Services.AddScoped<IJobService,       JobService>();

// ── Controllers & Swagger ──
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Licensing Service API", Version = "v1" }));

var app = builder.Build();

// ── Auto-migrate on startup ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicensingDbContext>();
    if (db.Database.IsRelational()) db.Database.Migrate();
    else db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { service = "licensing-service", status = "healthy" }));

app.Run();
