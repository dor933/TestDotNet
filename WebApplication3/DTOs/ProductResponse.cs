using System.Reflection;

namespace ProductInventoryApi.DTOs;

/// <summary>
/// Represents the response data transfer object for a product.
/// </summary>
public class ProductResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the product.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Gets or sets the name of the product.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Stock Keeping Unit (SKU) code of the product.
    /// </summary>
    public string SKU { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the price of the product.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the quantity of the product currently in stock.
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the category this product belongs to.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the name of the category this product belongs to.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the product was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the product is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Creates a <see cref="ProductResponse"/> from a <see cref="Models.Product"/> entity.
    /// </summary>
    /// <param name="product">The product entity to convert.</param>
    /// <returns>A new <see cref="ProductResponse"/> populated with data from the product entity.</returns>
    public static ProductResponse FromProduct(Models.Product product)
    {
        return new ProductResponse
        {
            ProductId = product.ProductId,
            Name = product.Name,
            SKU = product.SKU,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryId = product.CategoryId,
            CategoryName = product.CategoryName ?? string.Empty,
            CreatedAt = product.CreatedAt,
            IsActive = product.IsActive
        };
    }
}
