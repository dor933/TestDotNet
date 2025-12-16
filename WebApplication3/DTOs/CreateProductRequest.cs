using System.ComponentModel.DataAnnotations;

namespace ProductInventoryApi.DTOs;


public class CreateProductRequest
{
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Product name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "SKU is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "SKU must be between 1 and 50 characters.")]
    public string SKU { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required.")]
    [Range(0, double.MaxValue, ErrorMessage = "Price must be a non-negative value.")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Stock quantity is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity must be a non-negative value.")]
    public int StockQuantity { get; set; }

    [Required(ErrorMessage = "Category ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Category ID must be a positive integer.")]
    public int CategoryId { get; set; }
}

