using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Infrastructure.Adapters;
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
        else
            services.AddScoped<IErpAdapter, SqlErpAdapter>();

        return services;
    }
}
