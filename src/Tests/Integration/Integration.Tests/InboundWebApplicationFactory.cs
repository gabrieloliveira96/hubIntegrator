using Inbound.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Integration.Tests;

public class InboundWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RabbitMqContainer _rabbitMqContainer;
    private readonly RedisContainer _redisContainer;

    public InboundWebApplicationFactory(
        PostgreSqlContainer postgresContainer,
        RabbitMqContainer rabbitMqContainer,
        RedisContainer redisContainer)
    {
        _postgresContainer = postgresContainer;
        _rabbitMqContainer = rabbitMqContainer;
        _redisContainer = redisContainer;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<InboxDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add test database
            services.AddDbContext<InboxDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });

            // Remove existing Redis registration
            var redisDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor != null)
            {
                services.Remove(redisDescriptor);
            }

            // Add test Redis
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                return ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString());
            });

            // Update connection strings in configuration
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PostgreSQL"] = _postgresContainer.GetConnectionString(),
                    ["ConnectionStrings:RabbitMQ"] = _rabbitMqContainer.GetConnectionString(),
                    ["ConnectionStrings:Redis"] = _redisContainer.GetConnectionString(),
                    ["Seq:ServerUrl"] = "" // Disable Seq in tests
                });
            });
        });

        builder.UseEnvironment("Testing");
        
        // Disable Serilog Seq sink in tests to avoid connection errors
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });
    }
}

