using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Orchestrator.Worker.Infrastructure.Persistence;
using Orchestrator.Worker.Sagas;
using Orchestrator.Worker.Services;
using Serilog;
using Shared.Contracts;
using Shared.Observability;

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

// Serilog - Rich logging configuration
var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5342";
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Service", "Orchestrator.Worker")
    .Enrich.WithProperty("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{Service}] {Message:lj}{NewLine}  Properties: {Properties:j}{NewLine}  {Exception}")
    .WriteTo.Seq(seqUrl)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Database
builder.Services.AddDbContext<OrchestratorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// MassTransit with Saga
builder.Services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<RequestSaga, SagaStateMap>()
        .EntityFrameworkRepository(r =>
        {
            // Use Optimistic concurrency for PostgreSQL compatibility
            // Pessimistic mode can cause SQL syntax issues with PostgreSQL
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<OrchestratorDbContext>();
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672/";
        var uri = new Uri(rabbitMqConnectionString);
        
        // Virtual host: usar "/" (padrão) se estiver vazio
        var virtualHost = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(virtualHost))
        {
            virtualHost = "/";
        }
        
        cfg.Host(uri.Host, (ushort)uri.Port, virtualHost, h =>
        {
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var credentials = uri.UserInfo.Split(':');
                h.Username(credentials[0]);
                h.Password(credentials.Length > 1 ? credentials[1] : "");
            }
        });

        cfg.ConfigureEndpoints(context);

        // DLQ configuration
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        cfg.ReceiveEndpoint("request-received", e =>
        {
            e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(10)));
        });
    });
});

// Services
builder.Services.AddScoped<IBusinessRulesService, BusinessRulesService>();

// OpenTelemetry
builder.Services.AddOpenTelemetry(
    serviceName: "orchestrator-worker",
    otlpEndpoint: builder.Configuration["OpenTelemetry:OtlpEndpoint"]);

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL") ?? "")
    .AddRabbitMQ(rabbitConnectionString: builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672/");

var host = builder.Build();

// Migrations and cleanup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    db.Database.Migrate();
    
    // Cleanup invalid saga states
    await Orchestrator.Worker.Infrastructure.Persistence.SagaStateCleanup.CleanupInvalidStatesAsync(db, logger);
}

Log.Information("Orchestrator.Worker starting");

await host.RunAsync();
