using OpenTelemetry;
using OpenTelemetry.Metrics;
using Ride_Sharing_Backend.src.MatchmakingService.Hub;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry Metrics Collection
builder.Services.ConfigureOpenTelemetryMeterProvider(b =>
{
    b.AddAspNetCoreInstrumentation()
     .AddHttpClientInstrumentation()
     .AddMeter("MyApp");
});


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Load configuration from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Redis failover setup with multiple regions (you can use Redis Sentinel or managed Redis services)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse("localhost, region2-redis-endpoint"); // Multi-region Redis failover
    configuration.AllowAdmin = true;
    return ConnectionMultiplexer.Connect(configuration);
});


builder.Logging.ClearProviders();
builder.Logging.AddConsole(); // Structured console logging (can be sent to ELK)

// WebSocket service (for real-time updates)
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapHub<RideHub>("/rideHub");
app.Run();