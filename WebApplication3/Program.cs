using Microsoft.OpenApi.Models;
using ProductInventoryApi.Middleware;
using ProductInventoryApi.Repositories;
using ProductInventoryApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddSingleton<StockNotificationServer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<StockNotificationServer>());
builder.Services.AddHostedService<DailyCleanUpService>();

builder.Services.AddScoped<IProductRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<ProductRepository>>();
    var notificationServer = sp.GetRequiredService<StockNotificationServer>();
    return new ProductRepository(config, logger, notificationServer);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Product Inventory API",
        Version = "v1",
        Description = "A REST API for managing product inventory with categories. " +
                      "Features include product CRUD operations, stock management, " +
                      "rate limiting for write operations, and real-time stock update " +
                      "notifications via TCP socket server (port 5050). " +
                      "Connect using TcpClient to receive stock updates.",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@example.com"
        }
    });

});

var app = builder.Build();

app.EnsureDatabaseCreated("scheme.sql");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRateLimiting();


app.UseAuthorization();

app.MapControllers();

app.Run();
