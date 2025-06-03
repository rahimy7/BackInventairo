using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    // Request para login
    public class LoginRequest
    {
        [Required(ErrorMessage = "El usuario es requerido")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida")]
        public string Password { get; set; } = string.Empty;
    }

    // Response para login exitoso
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public UserInfo User { get; set; } = new UserInfo();
        public DateTime ExpiresAt { get; set; }
    }

    // Información del usuario
    public class UserInfo
    {
        public int Id { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
        public string Tienda { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        public DateTime? UltimoAcceso { get; set; }
        public DateTime FechaCreacion { get; set; }
        public List<string> DivisionesAsignadas { get; set; } = new List<string>();
    }

    // Respuesta genérica de la API
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T Data { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    // Enums para perfiles/roles de usuario
    public enum UserProfile
    {
        ADMINISTRADOR,
        GERENTE_TIENDA,
        LIDER,
        INVENTARIO
    }

    // Request para crear usuario
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "El usuario es requerido")]
        [StringLength(50, ErrorMessage = "El usuario no puede exceder 50 caracteres")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El email no es válido")]
        [StringLength(200, ErrorMessage = "El email no puede exceder 200 caracteres")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "El perfil es requerido")]
        public UserProfile Perfil { get; set; }

        [Required(ErrorMessage = "La tienda es requerida")]
        [StringLength(50, ErrorMessage = "La tienda no puede exceder 50 caracteres")]
        public string Tienda { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "El área no puede exceder 100 caracteres")]
        public string Area { get; set; } = string.Empty;

        public List<string> DivisionesAsignadas { get; set; } = new List<string>();
    }

    // Request para actualizar usuario
    public class UpdateUserRequest
    {
        [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string? Nombre { get; set; }

        [EmailAddress(ErrorMessage = "El email no es válido")]
        [StringLength(200, ErrorMessage = "El email no puede exceder 200 caracteres")]
        public string? Email { get; set; }

        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres")]
        public string? Password { get; set; }

        public UserProfile? Perfil { get; set; }

        [StringLength(50, ErrorMessage = "La tienda no puede exceder 50 caracteres")]
        public string? Tienda { get; set; }

        [StringLength(100, ErrorMessage = "El área no puede exceder 100 caracteres")]
        public string? Area { get; set; }

        public bool? Activo { get; set; }

        public List<string>? DivisionesAsignadas { get; set; }
    }

    // Request para obtener usuarios con filtros
    public class GetUsersRequest
    {
        public string SearchTerm { get; set; } = string.Empty;
        public UserProfile? Perfil { get; set; }
        public string Tienda { get; set; } = string.Empty;
        public bool? Activo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // Response completo de usuario
    public class UserResponse
    {
        public int Id { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserProfile Perfil { get; set; }
        public string Tienda { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public DateTime? UltimoAcceso { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }
        public List<string> DivisionesAsignadas { get; set; } = new List<string>();
    }

    // Actividad de usuario
    public class UserActivity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string Metadata { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public UserResponse User { get; set; } = new UserResponse();
    }

    // Request para verificar permisos
    public class CheckPermissionsRequest
    {
        [Required(ErrorMessage = "El permiso es requerido")]
        public string Permission { get; set; } = string.Empty;

        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }

    // Response de verificación de permisos
    public class PermissionCheckResponse
    {
        public bool HasPermission { get; set; }
        public string? Reason { get; set; }
    }
}