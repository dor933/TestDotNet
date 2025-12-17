using ProductInventoryApi.Models;

namespace WebApplication3.Interfaces;

/// <summary>
/// Defines the contract for product repository operations.
/// </summary>
public interface IProductsRepository
{
    /// <summary>
    /// Retrieves all active products, optionally filtered by category.
    /// </summary>
    /// <param name="categoryId">Optional category identifier to filter products.</param>
    /// <returns>A collection of active products.</returns>
    Task<IEnumerable<Product>> GetAllActiveProductsAsync(int? categoryId = null);

    /// <summary>
    /// Retrieves a product by its unique identifier.
    /// </summary>
    /// <param name="productId">The unique identifier of the product.</param>
    /// <returns>The product if found; otherwise, <c>null</c>.</returns>
    Task<Product?> GetProductByIdAsync(int productId);

    /// <summary>
    /// Creates a new product with the specified details.
    /// </summary>
    /// <param name="name">The name of the product.</param>
    /// <param name="sku">The Stock Keeping Unit (SKU) code of the product.</param>
    /// <param name="price">The price of the product.</param>
    /// <param name="stockQuantity">The initial stock quantity.</param>
    /// <param name="categoryId">The category identifier for the product.</param>
    /// <returns>A tuple containing the created product (if successful) and an error message (if failed).</returns>
    Task<(Product? Product, string? ErrorMessage)> CreateProductAsync(
        string name, string sku, decimal price, int stockQuantity, int categoryId);

    /// <summary>
    /// Updates the stock quantity for a specific product.
    /// </summary>
    /// <param name="productId">The unique identifier of the product.</param>
    /// <param name="newQuantity">The new stock quantity to set.</param>
    /// <returns>The updated product if found; otherwise, <c>null</c>.</returns>
    Task<Product?> UpdateStockAsync(int productId, int newQuantity);

    /// <summary>
    /// Performs a soft delete on a product by marking it as inactive.
    /// </summary>
    /// <param name="productId">The unique identifier of the product to delete.</param>
    /// <returns><c>true</c> if the product was successfully deleted; otherwise, <c>false</c>.</returns>
    Task<bool> SoftDeleteAsync(int productId);

    /// <summary>
    /// Checks whether a product with the specified SKU already exists.
    /// </summary>
    /// <param name="sku">The Stock Keeping Unit (SKU) code to check.</param>
    /// <returns><c>true</c> if a product with the SKU exists; otherwise, <c>false</c>.</returns>
    Task<bool> SkuExistsAsync(string sku);

    /// <summary>
    /// Increments the stock quantity for all products by two units.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task IncrementProducts();
}
