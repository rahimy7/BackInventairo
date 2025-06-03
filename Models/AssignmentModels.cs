using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    // Tipos de asignación disponibles
    public enum AssignmentType
    {
        DIVISION,
        CATEGORIA,
        GRUPO,
        SUBGRUPO
    }

    // Request para crear una asignación
    public class CreateAssignmentRequest
    {
        [Required(ErrorMessage = "El ID del usuario es requerido")]
        public int UserID { get; set; }

        [Required(ErrorMessage = "La tienda es requerida")]
        [StringLength(50, ErrorMessage = "La tienda no puede exceder 50 caracteres")]
        public string Tienda { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de asignación es requerido")]
        public AssignmentType AssignmentType { get; set; }

        // Campos específicos según el tipo de asignación
        public string DivisionCode { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public string Grupo { get; set; } = string.Empty;
        public string SubGroupCode { get; set; } = string.Empty;
        public string SubGrupo { get; set; } = string.Empty;
    }

    // Request para obtener asignaciones
    public class GetAssignmentsRequest
    {
        public int? UserID { get; set; }
        public string Tienda { get; set; } = string.Empty;
        public AssignmentType? AssignmentType { get; set; }
        public bool? IsActive { get; set; } = true;
    }

    // Response con la información de la asignación
    public class AssignmentResponse
    {
        public int ID { get; set; }
        public int UserID { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string NombreUsuario { get; set; } = string.Empty;
        public string Tienda { get; set; } = string.Empty;
        public AssignmentType AssignmentType { get; set; }
        
        // Información del producto asignado
        public ProductAssignmentInfo ProductInfo { get; set; }
        
        public int AssignedBy { get; set; }
        public string AssignedByName { get; set; } = string.Empty;
        public DateTime AssignedDate { get; set; }
        public bool IsActive { get; set; }
    }

    // Información del producto asignado
    public class ProductAssignmentInfo
    {
        public string DivisionCode { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public string Grupo { get; set; } = string.Empty;
        public string SubGroupCode { get; set; } = string.Empty;
        public string SubGrupo { get; set; } = string.Empty;
    }

    // Modelo para la vista de productos disponibles
    // Request para eliminar asignación
    public class DeleteAssignmentRequest
    {
        [Required(ErrorMessage = "El ID de la asignación es requerido")]
        public int AssignmentID { get; set; }
    }
}