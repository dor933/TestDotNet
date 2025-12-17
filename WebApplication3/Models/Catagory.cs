namespace ProductInventoryApi.Models;

/// <summary>
/// Represents a product category entity in the inventory system.
/// </summary>
public class Category
{
    /// <summary>
    /// Gets or sets the unique identifier of the category.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the name of the category.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the category.
    /// </summary>
    public string? Description { get; set; }
}
