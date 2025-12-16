using ProductInventoryApi.Models;

namespace ProductInventoryApi.Repositories;


public interface IProductRepository
{

    Task<IEnumerable<Product>> GetAllActiveProductsAsync(int? categoryId = null);


    Task<Product?> GetProductByIdAsync(int productId);


    Task<(Product? Product, string? ErrorMessage)> CreateProductAsync(
        string name, string sku, decimal price, int stockQuantity, int categoryId);


    Task<Product?> UpdateStockAsync(int productId, int newQuantity);


    Task<bool> SoftDeleteAsync(int productId);


    Task<bool> SkuExistsAsync(string sku);

    Task IncrementProducts();
}

