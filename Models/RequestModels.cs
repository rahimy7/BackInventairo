using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{


    public enum RequestStatus
    {
        PENDIENTE,
        EN_REVISION,
        LISTO,
        AJUSTADO,
        DEVUELTO,
        CANCELADO
    }

    // Prioridades de las solicitudes
    public enum RequestPriority
    {
        BAJA,
        NORMAL,
        ALTA,
        URGENTE
    }
    public enum HistoryAction
    {
        CREATED,
        ASSIGNED,
        STATUS_CHANGED,
        COMMENT,
        COMPLETED,
        AUTO_ASSIGNED
    }

    // Request para crear una nueva solicitud
    public class CreateRequestRequest
    {
        [Required(ErrorMessage = "La tienda es requerida")]
        [StringLength(50, ErrorMessage = "La tienda no puede exceder 50 caracteres")]
        public string Tienda { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los códigos son requeridos")]
        [MinLength(1, ErrorMessage = "Debe incluir al menos un código")]
        public List<string> ProductCodes { get; set; } = new List<string>();

        public RequestPriority Priority { get; set; } = RequestPriority.NORMAL;

        [StringLength(1000, ErrorMessage = "La descripción no puede exceder 1000 caracteres")]
        public string Description { get; set; } = string.Empty;

        public DateTime? DueDate { get; set; }
    }

    // Response de solicitud completa
    public class RequestResponse
    {
        public int ID { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public int RequestorID { get; set; }
        public string RequestorName { get; set; } = string.Empty;
        public string RequestorEmail { get; set; } = string.Empty;
        public string Tienda { get; set; } = string.Empty;
        public RequestStatus Status { get; set; }
        public RequestPriority Priority { get; set; }
        public string Description { get; set; } = string.Empty;
        public int TotalCodes { get; set; }
        public int CompletedCodes { get; set; }
        public int PendingCodes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public bool IsActive { get; set; }

        // Códigos asociados
        public List<RequestCodeResponse> Codes { get; set; } = new List<RequestCodeResponse>();

        // Estadísticas
        public RequestStatsInfo Stats { get; set; } = new RequestStatsInfo();
    }

    // Response de código individual
    public class RequestCodeResponse
    {
        public int ID { get; set; }
        public int RequestID { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public RequestStatus Status { get; set; }
        public int? AssignedToID { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public string AssignmentType { get; set; } = string.Empty;
        public string AssignmentInfo { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime? ProcessedDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }

    // Información de estadísticas
    public class RequestStatsInfo
    {
        public int PendingCodes { get; set; }
        public int InReviewCodes { get; set; }
        public int ReadyCodes { get; set; }
        public int AdjustedCodes { get; set; }
        public decimal CompletionPercentage { get; set; }
        public int DaysOpen { get; set; }
        public bool IsOverdue { get; set; }
    }

    // Request para obtener solicitudes con filtros
    public class GetRequestsRequest
    {
        public string Tienda { get; set; } = string.Empty;
        public RequestStatus? Status { get; set; }
        public RequestPriority? Priority { get; set; }
        public int? RequestorID { get; set; }
        public int? AssignedToID { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool? IsActive { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SearchTerm { get; set; } = string.Empty;
    }

    // Request para actualizar estado de código
    public class UpdateCodeStatusRequest
    {
        [Required(ErrorMessage = "El ID del código es requerido")]
        public int CodeID { get; set; }

        [Required(ErrorMessage = "El estado es requerido")]
        public RequestStatus Status { get; set; }

        [StringLength(500, ErrorMessage = "Las notas no pueden exceder 500 caracteres")]
        public string Notes { get; set; } = string.Empty;
    }

    // Request para agregar comentario
    public class AddCommentRequest
    {
        [Required(ErrorMessage = "El ID de la solicitud es requerido")]
        public int RequestID { get; set; }

        public int? CodeID { get; set; } // NULL para comentarios generales

        [Required(ErrorMessage = "El comentario es requerido")]
        [StringLength(1000, ErrorMessage = "El comentario no puede exceder 1000 caracteres")]
        public string Comment { get; set; } = string.Empty;
    }

    // Response de historial
    public class RequestHistoryResponse
    {
        public int ID { get; set; }
        public int RequestID { get; set; }
        public int? CodeID { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public int UserID { get; set; }
        public string UserName { get; set; } = string.Empty;
        public HistoryAction Action { get; set; }
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    // Response paginada
    public class PagedResponse<T>
    {
        public List<T> Data { get; set; } = new List<T>();
        public int TotalRecords { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    // Request para asignación manual
    public class AssignCodeRequest
    {
        [Required(ErrorMessage = "El ID del código es requerido")]
        public int CodeID { get; set; }

        [Required(ErrorMessage = "El ID del usuario es requerido")]
        public int AssignedToID { get; set; }

        [StringLength(500, ErrorMessage = "Las notas no pueden exceder 500 caracteres")]
        public string Notes { get; set; } = string.Empty;
    }

    // Resumen de dashboard
    public class RequestDashboardResponse
    {
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int InReviewRequests { get; set; }
        public int CompletedRequests { get; set; }
        public int OverdueRequests { get; set; }
        public int MyAssignedCodes { get; set; }
        public int MyPendingCodes { get; set; }

        public List<RequestsByTiendaStats> RequestsByTienda { get; set; } = new List<RequestsByTiendaStats>();
        public List<RequestsByStatusStats> RequestsByStatus { get; set; } = new List<RequestsByStatusStats>();
        public List<RequestResponse> RecentRequests { get; set; } = new List<RequestResponse>();
    }

    // Estadísticas por tienda
    public class RequestsByTiendaStats
    {
        public string Tienda { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int CompletedRequests { get; set; }
    }

    // Estadísticas por estado
    public class RequestsByStatusStats
    {
        public RequestStatus Status { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class BulkCreateRequest
    {
        public List<CreateRequestRequest> Requests { get; set; } = new();
    }

    public class BulkAssignCodesRequest
    {
        public List<int> CodeIDs { get; set; } = new();
        public int AssignedToID { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

public class BulkUpdateStatusRequest
{
    public List<int> CodeIDs { get; set; } = new();
    public RequestStatus Status { get; set; }
    public string Notes { get; set; } = string.Empty;
}


}