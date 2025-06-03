using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    // Estadísticas del sistema
    public class SystemStatsResponse
    {
        public int TotalUsers { get; set; }
        public int TotalStores { get; set; }
        public int ActiveRequests { get; set; }
        public int CompletedCounts { get; set; }
        public int PendingAssignments { get; set; }
        public decimal MonthlyGrowth { get; set; }
        public List<RoleStatsItem> UsersByRole { get; set; } = new List<RoleStatsItem>();
        public List<StatusStatsItem> RequestsByStatus { get; set; } = new List<StatusStatsItem>();
        public List<StorePerformanceItem> TopPerformingStores { get; set; } = new List<StorePerformanceItem>();
    }

    public class RoleStatsItem
    {
        public string Role { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class StatusStatsItem
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class StorePerformanceItem
    {
        public string Tienda { get; set; } = string.Empty;
        public decimal CompletionRate { get; set; }
    }

    // Gestión de tiendas
    public class StoreResponse
    {
        public string Tienda { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public int TotalUsuarios { get; set; }
        public int SolicitudesActivas { get; set; }
        public DateTime? UltimaActividad { get; set; }
    }

    // Gestión de divisiones
    public class DivisionResponse
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public string? Tienda { get; set; } // null = todas las tiendas
    }

    // Request para asignar divisiones a líder
    public class AssignDivisionsRequest
    {
        [Required(ErrorMessage = "El ID de usuario es requerido")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "La tienda es requerida")]
        public string Tienda { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los códigos de división son requeridos")]
        public List<string> DivisionCodes { get; set; } = new List<string>();
    }

    // Response de asignación de divisiones
    public class DivisionAssignmentResponse
    {
        public int UserId { get; set; }
        public string Tienda { get; set; } = string.Empty;
        public List<string> DivisionesAsignadas { get; set; } = new List<string>();
    }


    // Métricas de rendimiento
    public class PerformanceMetrics
    {
        public string Periodo { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaConsulta { get; set; }
        
        public SolicitudesMetrics Solicitudes { get; set; } = new SolicitudesMetrics();
        public ConteosMetrics Conteos { get; set; } = new ConteosMetrics();
        public UsuariosMetrics Usuarios { get; set; } = new UsuariosMetrics();
        public EficienciaMetrics Eficiencia { get; set; } = new EficienciaMetrics();
    }

    public class SolicitudesMetrics
    {
        public int Total { get; set; }
        public int Pendientes { get; set; }
        public int EnRevision { get; set; }
        public int Completadas { get; set; }
        public int Vencidas { get; set; }
        public decimal TasaCompletacion { get; set; }
    }

    public class ConteosMetrics
    {
        public int Total { get; set; }
        public int ConDiferencias { get; set; }
        public int SinDiferencias { get; set; }
        public int Pendientes { get; set; }
        public int Ajustados { get; set; }
        public decimal CostoDiferencias { get; set; }
        public decimal PorcentajePrecision { get; set; }
    }

    public class UsuariosMetrics
    {
        public int TotalActivos { get; set; }
        public int LoginsDiarios { get; set; }
        public List<object> UsuariosMasActivos { get; set; } = new List<object>();
    }

    public class EficienciaMetrics
    {
        public string TiempoPromedioSolicitud { get; set; } = string.Empty;
        public string TiempoPromedioConteo { get; set; } = string.Empty;
        public decimal SolicitudesPorUsuario { get; set; }
        public decimal ConteosPorUsuario { get; set; }
    }

    // Estadísticas de actividad de usuarios
    public class UserActivityStats
    {
        public int TotalUsuarios { get; set; }
        public int UsuariosActivos { get; set; }
        public int UsuariosInactivos { get; set; }
        public List<RoleDistribution> UsuariosPorPerfil { get; set; } = new List<RoleDistribution>();
        public List<StoreDistribution> UsuariosPorTienda { get; set; } = new List<StoreDistribution>();
        public List<RecentAccess> UltimosAccesos { get; set; } = new List<RecentAccess>();
        public List<InactiveUser> UsuariosSinAccesoReciente { get; set; } = new List<InactiveUser>();
    }

    public class RoleDistribution
    {
        public string Perfil { get; set; } = string.Empty;
        public int Cantidad { get; set; }
    }

    public class StoreDistribution
    {
        public string Tienda { get; set; } = string.Empty;
        public int Cantidad { get; set; }
    }

    public class RecentAccess
    {
        public string Usuario { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public DateTime? UltimoAcceso { get; set; }
        public string Tienda { get; set; } = string.Empty;
    }

    public class InactiveUser
    {
        public string Usuario { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public DateTime? UltimoAcceso { get; set; }
        public string Tienda { get; set; } = string.Empty;
        public int DiasInactivo { get; set; }
    }

    // Comparación entre tiendas
    public class StoreComparison
    {
        public string Tienda { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int TotalUsuarios { get; set; }
        public int UsuariosActivos { get; set; }
        public int SolicitudesActivas { get; set; }
        public DateTime? UltimaActividad { get; set; }
        public int TotalConteos { get; set; }
        public int ConteosConDiferencias { get; set; }
        public int ConteosPendientes { get; set; }
        public decimal PorcentajePrecision { get; set; }
        public decimal CostoDiferencias { get; set; }
        public Dictionary<string, int> DistribucionPerfiles { get; set; } = new Dictionary<string, int>();
    }

    // Resumen ejecutivo
    public class ExecutiveSummary
    {
        public DateTime FechaReporte { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public ExecutiveKPIs KPIs { get; set; } = new ExecutiveKPIs();
        public ExecutiveTrends Tendencias { get; set; } = new ExecutiveTrends();
        public List<ExecutiveAlert> Alertas { get; set; } = new List<ExecutiveAlert>();
        public List<StorePerformanceItem> TopPerformers { get; set; } = new List<StorePerformanceItem>();
    }

    public class ExecutiveKPIs
    {
        public int TotalUsuarios { get; set; }
        public int TotalTiendas { get; set; }
        public int SolicitudesActivas { get; set; }
        public int ConteosCompletados { get; set; }
        public decimal TasaCompletacionSolicitudes { get; set; }
        public decimal TasaPrecisionConteos { get; set; }
        public decimal ImpactoFinanciero { get; set; }
    }

    public class ExecutiveTrends
    {
        public decimal CrecimientoMensual { get; set; }
        public int SolicitudesVencidas { get; set; }
        public int ConteosPendientes { get; set; }
    }

    public class ExecutiveAlert
    {
        public string Tipo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string Prioridad { get; set; } = string.Empty;
    }

    // Estado de salud del sistema
    public class SystemHealth
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public long Uptime { get; set; }
        public ServiceHealth Services { get; set; } = new ServiceHealth();
        public MemoryInfo Memory { get; set; } = new MemoryInfo();
    }

    public class ServiceHealth
    {
        public string Database { get; set; } = string.Empty;
        public string InnovacentroDb { get; set; } = string.Empty;
        public string Authentication { get; set; } = string.Empty;
        public string Api { get; set; } = string.Empty;
    }

    public class MemoryInfo
    {
        public long UsedMB { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
    }
}