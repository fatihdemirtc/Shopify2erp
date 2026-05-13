using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Infrastructure.Adapters;
using ShopifyErpSync.Infrastructure.Adapters.Odoo;
using ShopifyErpSync.Infrastructure.Data;
using ShopifyErpSync.Infrastructure.External;
using ShopifyErpSync.Infrastructure.Outbox;
using ShopifyErpSync.Infrastructure.Services;

namespace ShopifyErpSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<OutboxMessageProcessor>();
        services.AddScoped<IOrderSyncService, OrderSyncService>();
        services.AddScoped<INotificationService, SlackNotifier>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddHttpClient("Slack");

        var adapterType = configuration["Erp:Adapter"];
        if (adapterType == "RestApi")
            services.AddScoped<IErpAdapter, RestApiErpAdapter>();
        else if (adapterType == "Odoo")
        {
            services.Configure<OdooSettings>(configuration.GetSection("Odoo"));
            services.AddSingleton<OdooClient>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<OdooSettings>>().Value;
                var logger = sp.GetRequiredService<ILogger<OdooClient>>();
                var http = new HttpClient
                {
                    BaseAddress = new Uri(settings.BaseUrl),
                    Timeout = TimeSpan.FromSeconds(30)
                };
                return new OdooClient(http, settings, logger);
            });
            services.AddScoped<IErpAdapter, OdooErpAdapter>();
        }
        else
            services.AddScoped<IErpAdapter, SqlErpAdapter>();

        return services;
    }
}
