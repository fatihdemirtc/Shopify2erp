using ShopifyErpSync.Api.Middleware;
using ShopifyErpSync.Infrastructure;
using ShopifyErpSync.Infrastructure.Data;
using ShopifyErpSync.Infrastructure.Outbox;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OutboxProcessor>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<HmacVerificationMiddleware>();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}

app.Run();
