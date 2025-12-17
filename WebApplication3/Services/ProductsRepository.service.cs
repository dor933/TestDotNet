using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using ProductInventoryApi.Models;
using ProductInventoryApi.Services;
using WebApplication3.Interfaces;

namespace ProductInventoryApi.Repositories;

/// <summary>
/// Repository for managing product data operations using Dapper and SQL Server.
/// </summary>
public class ProductsRepository : IProductsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ProductsRepository> _logger;
    private readonly StockNotificationServer? _notificationServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductsRepository"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration to retrieve connection strings.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notificationServer">Optional reference to the stock notification server for broadcasting updates.</param>
    /// <exception cref="InvalidOperationException">Thrown if the 'DefaultConnection' string is missing.</exception>
    public ProductsRepository(
        IConfiguration configuration,
        ILogger<ProductsRepository> logger,
        StockNotificationServer? notificationServer = null)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _logger = logger;
        _notificationServer = notificationServer;
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    /// <summary>
    /// Retrieves all active products, optionally filtered by category.
    /// </summary>
    /// <param name="categoryId">Optional category identifier to filter products.</param>
    /// <returns>A collection of active products.</returns>
    public async Task<IEnumerable<Product>> GetAllActiveProductsAsync(int? categoryId = null)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            // Using stored procedure for this operation
            var parameters = new DynamicParameters();
            parameters.Add("@CategoryId", categoryId, DbType.Int32);

            var products = await connection.QueryAsync<Product>(
                "sp_GetAllActiveProducts",
                parameters,
                commandType: CommandType.StoredProcedure);

            return products;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active products. CategoryId: {CategoryId}", categoryId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a product by its unique identifier.
    /// </summary>
    /// <param name="productId">The unique identifier of the product.</param>
    /// <returns>The product if found; otherwise, <c>null</c>.</returns>
    public async Task<Product?> GetProductByIdAsync(int productId)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            // Using parameterized query to prevent SQL injection
            const string sql = @"
                SELECT 
                    p.ProductId,
                    p.Name,
                    p.SKU,
                    p.Price,
                    p.StockQuantity,
                    p.CategoryId,
                    c.CategoryName,
                    p.CreatedAt,
                    p.IsActive
                FROM Products p
                INNER JOIN Categories c ON p.CategoryId = c.CategoryId
                WHERE p.ProductId = @ProductId AND p.IsActive = 1";

            var product = await connection.QueryFirstOrDefaultAsync<Product>(
                sql,
                new { ProductId = productId });

            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product by ID: {ProductId}", productId);
            throw;
        }
    }

    /// <summary>
    /// Increments the stock quantity for all products by two units.
    /// Broadcasts a maintenance stock update notification if the notification server is available.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task IncrementProducts()
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            await connection.QueryAsync<Product>(
                "sp_IncreaseAllStockByTwo",
                commandType: CommandType.StoredProcedure
                );

            if(_notificationServer != null)
            {
               await _notificationServer.BroadcastMaintananceStockUpdate();
            }
        }
        catch(Exception ex) 
        {
            _logger.LogError(ex, "Error in daily increment of product quantities");
            throw;

        }
    }

    /// <summary>
    /// Creates a new product with the specified details.
    /// </summary>
    /// <param name="name">The name of the product.</param>
    /// <param name="sku">The Stock Keeping Unit (SKU) code of the product.</param>
    /// <param name="price">The price of the product.</param>
    /// <param name="stockQuantity">The initial stock quantity.</param>
    /// <param name="categoryId">The category identifier for the product.</param>
    /// <returns>A tuple containing the created product (if successful) and an error message (if failed).</returns>
    public async Task<(Product? Product, string? ErrorMessage)> CreateProductAsync(
        string name, string sku, decimal price, int stockQuantity, int categoryId)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            // Using stored procedure for product creation
            var parameters = new DynamicParameters();
            parameters.Add("@Name", name, DbType.String, size: 200);
            parameters.Add("@SKU", sku, DbType.String, size: 50);
            parameters.Add("@Price", price, DbType.Decimal);
            parameters.Add("@StockQuantity", stockQuantity, DbType.Int32);
            parameters.Add("@CategoryId", categoryId, DbType.Int32);
            parameters.Add("@ProductId", dbType: DbType.Int32, direction: ParameterDirection.Output);
            parameters.Add("@ErrorMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

            var product = await connection.QueryFirstOrDefaultAsync<Product>(
                "sp_CreateProduct",
                parameters,
                commandType: CommandType.StoredProcedure);

            var errorMessage = parameters.Get<string?>("@ErrorMessage");
            var productId = parameters.Get<int>("@ProductId");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                return (null, errorMessage);
            }

            return (product, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product. SKU: {SKU}", sku);
            throw;
        }
    }

    /// <summary>
    /// Updates the stock quantity for a specific product.
    /// Broadcasts a stock update notification if the notification server is available.
    /// </summary>
    /// <param name="productId">The unique identifier of the product.</param>
    /// <param name="newQuantity">The new stock quantity to set.</param>
    /// <returns>The updated product if found; otherwise, <c>null</c>.</returns>
    public async Task<Product?> UpdateStockAsync(int productId, int newQuantity)
    {
        try
        {
            // Get old quantity for notification
            var oldProduct = await GetProductByIdAsync(productId);
            var oldQuantity = oldProduct?.StockQuantity ?? 0;

            using var connection = CreateConnection();
            await connection.OpenAsync();

            var parameters = new DynamicParameters();
            parameters.Add("@ProductId", productId, DbType.Int32);
            parameters.Add("@NewQuantity", newQuantity, DbType.Int32);

            var product = await connection.QueryFirstOrDefaultAsync<Product>(
                "sp_UpdateProductStock",
                parameters,
                commandType: CommandType.StoredProcedure);

            if (product != null && _notificationServer != null)
            {
                await _notificationServer.BroadcastStockUpdateAsync(
                    product.ProductId,
                    product.Name,
                    oldQuantity,
                    newQuantity);
            }

            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock for product ID: {ProductId}", productId);
            throw;
        }
    }

    /// <summary>
    /// Performs a soft delete on a product by marking it as inactive.
    /// </summary>
    /// <param name="productId">The unique identifier of the product to delete.</param>
    /// <returns><c>true</c> if the product was successfully deleted; otherwise, <c>false</c>.</returns>
    public async Task<bool> SoftDeleteAsync(int productId)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                UPDATE Products 
                SET IsActive = 0 
                WHERE ProductId = @ProductId AND IsActive = 1";

            var rowsAffected = await connection.ExecuteAsync(sql, new { ProductId = productId });
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft deleting product ID: {ProductId}", productId);
            throw;
        }
    }

    /// <summary>
    /// Checks whether a product with the specified SKU already exists.
    /// </summary>
    /// <param name="sku">The Stock Keeping Unit (SKU) code to check.</param>
    /// <returns><c>true</c> if a product with the SKU exists; otherwise, <c>false</c>.</returns>
    public async Task<bool> SkuExistsAsync(string sku)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            const string sql = "SELECT COUNT(1) FROM Products WHERE SKU = @SKU";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { SKU = sku });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SKU existence: {SKU}", sku);
            throw;
        }
    }
}
