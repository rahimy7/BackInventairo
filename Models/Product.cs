using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioAPI.Models
{
    // Entidad Product principal
    public class Product
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DivisionCode { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Description1 { get; set; } = string.Empty;
        public string Description2 { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string SubGroupCode { get; set; } = string.Empty;
        public string SubGroup { get; set; } = string.Empty;
        public string BrandCode { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime UpdatedDate { get; set; } = DateTime.Now;
    }

    // Vista de productos desde INNOVACENTRO
    // DTO para respuesta de producto
    public class ProductResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Description1 { get; set; } = string.Empty;
        public string Description2 { get; set; } = string.Empty;
        public string DivisionCode { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string SubGroupCode { get; set; } = string.Empty;
        public string SubGroup { get; set; } = string.Empty;
        public string BrandCode { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public string VendorNo { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ItemClasification { get; set; } = string.Empty;
        public string ItemStockClasification { get; set; } = string.Empty;
        public string CentroAbastecimiento { get; set; } = string.Empty;
        public string CentroAbastecimiento2 { get; set; } = string.Empty;
        public bool IsBlocked { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastDateModified { get; set; }
        public DateTime? CreatedDate { get; set; }
    }

    // Request para búsqueda de productos
    public class ProductSearchRequest
    {
        public string SearchTerm { get; set; } = string.Empty;
        public string DivisionCode { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public string SubGroupCode { get; set; } = string.Empty;
        public string BrandCode { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
        public bool? IsBlocked { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // Jerarquía de productos
    public class ProductHierarchyItem
    {
        public string DivisionCode { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public string Grupo { get; set; } = string.Empty;
        public string SubGroupCode { get; set; } = string.Empty;
        public string SubGrupo { get; set; } = string.Empty;
        public int ProductCount { get; set; }
    }

    // Estadísticas de productos
    public class ProductStats
    {
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int BlockedProducts { get; set; }
        public int ProductsWithStock { get; set; }
        public int ProductsWithoutStock { get; set; }
        public List<DivisionStats> ProductsByDivision { get; set; } = new List<DivisionStats>();
        public List<CategoryStats> ProductsByCategory { get; set; } = new List<CategoryStats>();
        public List<BrandStats> ProductsByBrand { get; set; } = new List<BrandStats>();
    }

    public class DivisionStats
    {
        public string DivisionCode { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal AverageCost { get; set; }
    }

    public class CategoryStats
    {
        public string CategoryCode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal AverageCost { get; set; }
    }

    public class BrandStats
    {
        public string BrandCode { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal AverageCost { get; set; }
    }

    // Request para importación masiva de productos
    public class BulkProductImportRequest
    {
        public List<ProductImportItem> Products { get; set; } = new List<ProductImportItem>();
        public bool UpdateExisting { get; set; } = true;
        public bool ValidateOnly { get; set; } = false;
    }

    public class ProductImportItem
    {
        public string Code { get; set; } = string.Empty;
        public string Description1 { get; set; } = string.Empty;
        public string Description2 { get; set; } = string.Empty;
        public string DivisionCode { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string SubGroupCode { get; set; } = string.Empty;
        public string SubGroup { get; set; } = string.Empty;
        public string BrandCode { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public string VendorNo { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    // Response de importación masiva
    public class BulkProductImportResponse
    {
        public int TotalProcessed { get; set; }
        public int SuccessfulImports { get; set; }
        public int FailedImports { get; set; }
        public int UpdatedProducts { get; set; }
        public int NewProducts { get; set; }
        public List<ProductImportError> Errors { get; set; } = new List<ProductImportError>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class ProductImportError
    {
        public int Row { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}