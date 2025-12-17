using System.ComponentModel.DataAnnotations;

namespace ProductInventoryApi.DTOs;

/// <summary>
/// Represents the request data transfer object for creating a new product.
/// </summary>
public class CreateProductRequest
{
    /// <summary>
    /// The display name of the product
    /// </summary>
    /// <example>Xiaomi 17 Smartphone</example>
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Product name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique Stock Keeping Unit identifier
    /// </summary>
    /// <example>XIAOMI-SMR-2025</example>
    [Required(ErrorMessage = "SKU is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "SKU must be between 1 and 50 characters.")]
    public string SKU { get; set; } = string.Empty;

    /// <summary>
    /// Price in USD
    /// </summary>
    /// <example>499.99</example>
    [Required(ErrorMessage = "Price is required.")]
    [Range(0, double.MaxValue, ErrorMessage = "Price must be a non-negative value.")]
    public decimal Price { get; set; }

    /// <summary>
    /// Initial stock count
    /// </summary>
    /// <example>150</example>
    [Required(ErrorMessage = "Stock quantity is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity must be a non-negative value.")]
    public int StockQuantity { get; set; }

    /// <summary>
    /// The identifier of the category this product belongs to.
    /// </summary>
    /// <example>1</example>
    [Required(ErrorMessage = "Category ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Category ID must be a positive integer.")]
    public int CategoryId { get; set; }
}
