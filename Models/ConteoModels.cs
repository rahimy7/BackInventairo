using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    // Enums para el sistema de conteo
    public enum CountStatus
    {
        EN_REVISION,
        DEVUELTO,
        FORENSE,
        AJUSTADO
    }

    public enum MovementType
    {
        AJUSTE_POSITIVO,
        AJUSTE_NEGATIVO,
        STOCK_CUADRADO
    }

    public enum CountAction
    {
        CREATED,
        COUNTED,
        STATUS_CHANGED,
        COMMENT_ADDED,
        ADJUSTED
    }

    // Request para crear conteos desde solicitud
    public class CreateInventoryCountsRequest
    {
        [Required(ErrorMessage = "El ID de la solicitud es requerido")]
        public int RequestID { get; set; }
    }

    // Request para registrar conteo físico
    public class RegisterPhysicalCountRequest
    {
        [Required(ErrorMessage = "El ID del conteo es requerido")]
        public int CountID { get; set; }

        [Required(ErrorMessage = "La cantidad física es requerida")]
        [Range(0, double.MaxValue, ErrorMessage = "La cantidad física debe ser mayor o igual a 0")]
        public decimal CantidadFisica { get; set; }

        [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres")]
        public string Comentario { get; set; } = string.Empty;
    }

    // Request para actualizar estado de conteo
    public class UpdateCountStatusRequest
    {
        [Required(ErrorMessage = "El ID del conteo es requerido")]
        public int CountID { get; set; }

        [Required(ErrorMessage = "El estado es requerido")]
        public CountStatus Estado { get; set; }

        [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres")]
        public string Comentario { get; set; } = string.Empty;
    }

    // Request para obtener conteos con filtros
    public class GetInventoryCountsRequest
    {
        public int? RequestID { get; set; }
        public string Tienda { get; set; } = string.Empty;
        public CountStatus? Estado { get; set; }
        public string EstatusCodigoFilter { get; set; } = string.Empty;
        public string DivisionCode { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? AssignedToID { get; set; }
        public bool? HasDifferences { get; set; } // Para filtrar solo los que tienen diferencias
        public bool? IsActive { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SearchTerm { get; set; } = string.Empty;
    }

    // Response completo de conteo
    public class InventoryCountResponse
    {
        public int ID { get; set; }
        public int RequestID { get; set; }
        public int CodeID { get; set; }
        public string Tienda { get; set; } = string.Empty;
        public string CodBarras { get; set; } = string.Empty;
        public string NoProducto { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Descripcion2 { get; set; } = string.Empty;
        public string CodDivision { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal StockCalculado { get; set; }
        public decimal? CantidadFisica { get; set; }
        public decimal Diferencia { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal CostoTotal { get; set; }
        public string Comentario { get; set; } = string.Empty;
        public MovementType TipoMovimiento { get; set; }
        public string EstatusCodigoFilter { get; set; } = string.Empty;
        public string Ticket { get; set; } = string.Empty;
        public CountStatus Estado { get; set; }
        public DateTime Fecha { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public bool IsActive { get; set; }

        // Información adicional de la solicitud
        public string TicketNumber { get; set; } = string.Empty;
        public string RequestorName { get; set; } = string.Empty;
        public RequestPriority RequestPriority { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;

        // Información adicional del producto
        public string ProductDescription { get; set; } = string.Empty;
        public string ProductDescription2 { get; set; } = string.Empty;
        public string ProductDivisionCode { get; set; } = string.Empty;
        public string ItemCategoryCode { get; set; } = string.Empty;
        public string ProductGroupCode { get; set; } = string.Empty;
        public string UnitMeasureCode { get; set; } = string.Empty;
        public string ProductStatus { get; set; } = string.Empty;
        public string ItemClasification { get; set; } = string.Empty;
        public string ItemStockClasification { get; set; } = string.Empty;
        public decimal ItemUnitPrice { get; set; }
        public decimal ItemUnitCost { get; set; }
        public string CentroAbastecimiento { get; set; } = string.Empty;
        public string CentroAbastecimiento2 { get; set; } = string.Empty;

        // Indicadores calculados
        public bool HasDifference => Math.Abs(Diferencia) > 0.01m;
        public bool IsPhysicalCountRegistered => CantidadFisica.HasValue;
        public string DiferenceType => Diferencia > 0 ? "EXCESO" : Diferencia < 0 ? "FALTANTE" : "CUADRADO";
    }

    // Response de historial de conteo
    public class InventoryCountHistoryResponse
    {
        public int ID { get; set; }
        public int CountID { get; set; }
        public int UserID { get; set; }
        public string UserName { get; set; } = string.Empty;
        public CountAction Action { get; set; }
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    // Response de dashboard de conteos
    public class InventoryCountDashboardResponse
    {
        public int TotalCounts { get; set; }
        public int CountsEnRevision { get; set; }
        public int CountsDevueltos { get; set; }
        public int CountsForenses { get; set; }
        public int CountsAjustados { get; set; }
        public int CountsPendientes { get; set; } // Sin contar
        public int CountsWithDifferences { get; set; }
        public int CountsWithoutDifferences { get; set; }
        
        // Estadísticas financieras
        public decimal TotalCostoDiferencias { get; set; }
        public decimal CostoAjustesPositivos { get; set; }
        public decimal CostoAjustesNegativos { get; set; }
        
        // Estadísticas por tienda
        public List<CountsByTiendaStats> CountsByTienda { get; set; } = new List<CountsByTiendaStats>();
        
        // Estadísticas por división
        public List<CountsByDivisionStats> CountsByDivision { get; set; } = new List<CountsByDivisionStats>();
        
        // Estadísticas por estado
        public List<CountsByStatusStats> CountsByStatus { get; set; } = new List<CountsByStatusStats>();
        
        // Estadísticas por tipo de movimiento
        public List<CountsByMovementTypeStats> CountsByMovementType { get; set; } = new List<CountsByMovementTypeStats>();
        
        // Conteos recientes
        public List<InventoryCountResponse> RecentCounts { get; set; } = new List<InventoryCountResponse>();
    }

    // Estadísticas por tienda
    public class CountsByTiendaStats
    {
        public string Tienda { get; set; } = string.Empty;
        public int TotalCounts { get; set; }
        public int CountsWithDifferences { get; set; }
        public decimal TotalCostoDiferencias { get; set; }
        public decimal CompletionPercentage { get; set; }
    }

    // Estadísticas por división
    public class CountsByDivisionStats
    {
        public string DivisionCode { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public int TotalCounts { get; set; }
        public int CountsWithDifferences { get; set; }
        public decimal TotalCostoDiferencias { get; set; }
    }

    // Estadísticas por estado
    public class CountsByStatusStats
    {
        public CountStatus Estado { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public decimal TotalCosto { get; set; }
    }

    // Estadísticas por tipo de movimiento
    public class CountsByMovementTypeStats
    {
        public MovementType TipoMovimiento { get; set; }
        public int Count { get; set; }
        public decimal TotalCosto { get; set; }
        public decimal Percentage { get; set; }
    }

    // Request para agregar comentario a conteo
    public class AddCountCommentRequest
    {
        [Required(ErrorMessage = "El ID del conteo es requerido")]
        public int CountID { get; set; }

        [Required(ErrorMessage = "El comentario es requerido")]
        [StringLength(1000, ErrorMessage = "El comentario no puede exceder 1000 caracteres")]
        public string Comment { get; set; } = string.Empty;
    }

    // Request para actualización masiva de conteos
    public class BatchUpdateCountsRequest
    {
        [Required(ErrorMessage = "La lista de conteos es requerida")]
        [MinLength(1, ErrorMessage = "Debe incluir al menos un conteo")]
        public List<RegisterPhysicalCountRequest> Counts { get; set; } = new List<RegisterPhysicalCountRequest>();
    }

    // Response de resumen de conteo para vista rápida
    public class InventoryCountSummaryResponse
    {
        public int ID { get; set; }
        public string CodBarras { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Tienda { get; set; } = string.Empty;
        public decimal StockCalculado { get; set; }
        public decimal? CantidadFisica { get; set; }
        public decimal Diferencia { get; set; }
        public decimal CostoTotal { get; set; }
        public CountStatus Estado { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public bool IsPhysicalCountRegistered => CantidadFisica.HasValue;
        public bool HasDifference => Math.Abs(Diferencia) > 0.01m;
        public string StatusColor => Estado switch
        {
            CountStatus.EN_REVISION => "warning",
            CountStatus.DEVUELTO => "danger",
            CountStatus.FORENSE => "info",
            CountStatus.AJUSTADO => "success",
            _ => "secondary"
        };
    }
}