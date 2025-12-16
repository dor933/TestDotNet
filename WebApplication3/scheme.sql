
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL
    DROP TABLE dbo.Products;
GO

IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL
    DROP TABLE dbo.Categories;
GO

CREATE TABLE dbo.Categories (
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500) NULL
);
GO


CREATE TABLE dbo.Products (
    ProductId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    SKU NVARCHAR(50) NOT NULL UNIQUE,
    Price DECIMAL(18, 2) NOT NULL CHECK (Price >= 0),
    StockQuantity INT NOT NULL DEFAULT 0 CHECK (StockQuantity >= 0),
    CategoryId INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Products_Categories 
        FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId)
);
GO

-- Index: Frequently queried fields
CREATE NONCLUSTERED INDEX IX_Products_CategoryId 
    ON dbo.Products(CategoryId) 
    WHERE IsActive = 1;
GO

CREATE NONCLUSTERED INDEX IX_Products_SKU 
    ON dbo.Products(SKU);
GO

CREATE NONCLUSTERED INDEX IX_Products_IsActive 
    ON dbo.Products(IsActive) 
    INCLUDE (Name, Price, StockQuantity, CategoryId);
GO


IF OBJECT_ID('dbo.sp_GetAllActiveProducts', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetAllActiveProducts;
GO

CREATE PROCEDURE dbo.sp_GetAllActiveProducts
    @CategoryId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
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
    FROM dbo.Products p
    INNER JOIN dbo.Categories c ON p.CategoryId = c.CategoryId
    WHERE p.IsActive = 1
        AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
    ORDER BY p.Name;
END;
GO



IF OBJECT_ID('dbo.sp_IncreaseAllStockByTwo', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_IncreaseAllStockByTwo;
GO

CREATE PROCEDURE dbo.sp_IncreaseAllStockByTwo
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Update stock quantity for all products (will run each day at 02:00 AM )
        UPDATE dbo.Products
        SET StockQuantity = StockQuantity + 2;
                
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END;
GO

IF OBJECT_ID('dbo.sp_CreateProduct', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_CreateProduct;
GO

CREATE PROCEDURE dbo.sp_CreateProduct
    @Name NVARCHAR(200),
    @SKU NVARCHAR(50),
    @Price DECIMAL(18, 2),
    @StockQuantity INT,
    @CategoryId INT,
    @ProductId INT OUTPUT,
    @ErrorMessage NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Initialize outputs
    SET @ProductId = 0;
    SET @ErrorMessage = NULL;
    
    BEGIN TRY
        -- Check if SKU already exists
        IF EXISTS (SELECT 1 FROM dbo.Products WHERE SKU = @SKU)
        BEGIN
            SET @ErrorMessage = 'A product with this SKU already exists.';
            RETURN -1;
        END
        
        -- Check if CategoryId is valid
        IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryId = @CategoryId)
        BEGIN
            SET @ErrorMessage = 'Invalid CategoryId. Category does not exist.';
            RETURN -2;
        END
        
        -- Insert the product
        INSERT INTO dbo.Products (Name, SKU, Price, StockQuantity, CategoryId)
        VALUES (@Name, @SKU, @Price, @StockQuantity, @CategoryId);
        
        SET @ProductId = SCOPE_IDENTITY();
        
        -- Return the created product
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
        FROM dbo.Products p
        INNER JOIN dbo.Categories c ON p.CategoryId = c.CategoryId
        WHERE p.ProductId = @ProductId;
        
        RETURN 0;
    END TRY
    BEGIN CATCH
        SET @ErrorMessage = ERROR_MESSAGE();
        RETURN -99;
    END CATCH
END;
GO


IF OBJECT_ID('dbo.sp_UpdateProductStock', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_UpdateProductStock;
GO

CREATE PROCEDURE dbo.sp_UpdateProductStock
    @ProductId INT,
    @NewQuantity INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Update stock quantity
    UPDATE dbo.Products
    SET StockQuantity = @NewQuantity
    WHERE ProductId = @ProductId AND IsActive = 1;
    
    -- Return updated product
    IF @@ROWCOUNT > 0
    BEGIN
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
        FROM dbo.Products p
        INNER JOIN dbo.Categories c ON p.CategoryId = c.CategoryId
        WHERE p.ProductId = @ProductId;
    END
END;
GO

-- Sample Data

INSERT INTO dbo.Categories (CategoryName, Description)
VALUES 
    ('Electronics', 'Electronic devices and accessories including smartphones, laptops, and gadgets'),
    ('Clothing', 'Apparel and fashion items for men, women, and children'),
    ('Home & Garden', 'Products for home improvement, decoration, and gardening');
GO

INSERT INTO dbo.Products (Name, SKU, Price, StockQuantity, CategoryId)
VALUES 
    ('Wireless Bluetooth Headphones', 'ELEC-WBH-001', 79.99, 150, 1),
    ('USB-C Charging Cable', 'ELEC-USB-002', 12.99, 500, 1),
    ('Mens Cotton T-Shirt', 'CLTH-MTS-001', 24.99, 200, 2),
    ('Womens Running Shoes', 'CLTH-WRS-002', 89.99, 75, 2),
    ('Indoor Plant Pot Set', 'HOME-PPS-001', 34.99, 100, 3);
GO

PRINT 'Database schema created successfully with sample data.';
GO

