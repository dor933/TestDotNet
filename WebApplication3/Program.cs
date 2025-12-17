using Microsoft.OpenApi.Models;
using ProductInventoryApi.Middleware;
using ProductInventoryApi.Repositories;
using ProductInventoryApi.Services;
using WebApplication3.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddSingleton<StockNotificationServer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<StockNotificationServer>());
builder.Services.AddHostedService<DailyCleanUpService>();

builder.Services.AddScoped<IProductsService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<ProductsService>>();
    var notificationServer = sp.GetRequiredService<StockNotificationServer>();
    return new ProductsService(config, logger, notificationServer);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Product Inventory API",
        Version = "v1",
        Description = @"Product Inventory API - User Guide

This API allows you to manage the product inventory. Below are the instructions for using the available endpoints via this UI.

1. Create a New Product
Method: POST /api/Products
Description: Adds a new product to the inventory.
Example Body:
{
  ""name"": ""Gaming Laptop X1"",
  ""sku"": ""GL-X1-2024"",
  ""price"": 4500.00,
  ""stockQuantity"": 15,
  ""categoryId"": 1
}

2. Get All Products
Method: GET /api/Products
Description: Retrieves a list of all active products.
Filtering: You can use the optional categoryId parameter to filter the results.

3. Get Product by ID
Method: GET /api/Products/{id}
Description: Retrieves details of a specific product by its unique ID.

4. Update Stock
Method: PUT /api/Products/{id}/stock
Description: Updates the stock quantity for an existing product.
Example Body:
{
  ""quantity"": 50
}

5. Delete Product
Method: DELETE /api/Products/{id}
Description: Performs a Soft Delete. The product is marked as inactive and will no longer appear in the main list, but it is not physically removed from the database.",
    
    Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "doratzabi1@gmail.com"
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
