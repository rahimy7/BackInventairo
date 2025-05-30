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
        public string Perfil { get; set; } = string.Empty;
        public string Tienda { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
    }

    // Respuesta genérica de la API
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T Data { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}