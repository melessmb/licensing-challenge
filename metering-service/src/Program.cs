using StackExchange.Redis;
using MeteringService.Services.Interfaces;
using MeteringService.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// ── Redis ──
var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConn));

// ── Services ──
builder.Services.AddSingleton<IMeteringService, RedisMeteringService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Metering Service API", Version = "v1" }));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { service = "metering-service", status = "healthy" }));

app.Run();
