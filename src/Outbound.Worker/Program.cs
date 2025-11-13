using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Outbound.Worker.Consumers;
using Shared.Policies;
using Outbound.Worker.Infrastructure.Http;
using Outbound.Worker.Infrastructure.Persistence;
using Serilog;
using Shared.Observability;
using System.Net.Http;

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

// Serilog
var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Service", "Outbound.Worker")
    .Enrich.WithProperty("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{Service}] {Message:lj}{NewLine}  Properties: {Properties:j}{NewLine}  {Exception}")
    .WriteTo.Seq(seqUrl)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Database
builder.Services.AddDbContext<OutboxDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// HttpClient with Polly
// In development, ignore SSL certificate validation for mock endpoints
var isDevelopment = builder.Environment.EnvironmentName == "Development";
var httpClientHandler = new HttpClientHandler();
if (isDevelopment)
{
    httpClientHandler.ServerCertificateCustomValidationCallback = 
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
}

builder.Services.AddHttpClient("ThirdParty", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "IntegrationHub/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => httpClientHandler);

builder.Services.AddScoped<IThirdPartyClient, ThirdPartyClient>();

// MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DispatchToPartnerConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("dispatch-to-partner", e =>
        {
            e.ConfigureConsumer<DispatchToPartnerConsumer>(context);
            e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(10)));
        });

        cfg.ConfigureEndpoints(context);
    });
});

// Outbox Dispatcher
builder.Services.AddHostedService<OutboxDispatcher>();

// OpenTelemetry
builder.Services.AddOpenTelemetry(
    serviceName: "outbound-worker",
    otlpEndpoint: builder.Configuration["OpenTelemetry:OtlpEndpoint"]);

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL") ?? "")
    .AddRabbitMQ(rabbitConnectionString: builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672/");

var host = builder.Build();

// Migrations
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
    db.Database.Migrate();
}

Log.Information("Outbound.Worker starting");

host.Run();
