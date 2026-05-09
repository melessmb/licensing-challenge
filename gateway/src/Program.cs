var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Licensing Cloud — Gateway", Version = "v1" }));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapReverseProxy();
app.MapGet("/health", () => Results.Ok(new
{
    service   = "gateway",
    status    = "healthy",
    timestamp = DateTime.UtcNow
}));

app.Run();
