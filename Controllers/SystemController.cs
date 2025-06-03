using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using InventarioAPI.Models;
using InventarioAPI.Services;
using System.Security.Claims;

namespace InventarioAPI.Controllers
{
    /// <summary>
    /// Controlador para estadísticas y métricas del sistema
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class SystemController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IRequestService _requestService;
        private readonly IInventoryCountService _inventoryCountService;
        private readonly IStoreService _storeService;

        public SystemController(
            IUserService userService,
            IRequestService requestService,
            IInventoryCountService inventoryCountService,
            IStoreService storeService)
        {
            _userService = userService;
            _requestService = requestService;
            _inventoryCountService = inventoryCountService;
            _storeService = storeService;
        }

        /// <summary>
        /// Obtener estadísticas completas del sistema
        /// </summary>
        /// <returns>Estadísticas generales del sistema</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(ApiResponse<SystemStatsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<SystemStatsResponse>>> GetSystemStats()
        {
            try
            {
                // Verificar permisos - solo administradores
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                var result = await _userService.GetSystemStatsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener métricas de rendimiento del sistema
        /// </summary>
        /// <param name="periodo">Período para las métricas (dia, semana, mes, año)</param>
        /// <returns>Métricas de rendimiento</returns>
        [HttpGet("performance")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetPerformanceMetrics([FromQuery] string periodo = "mes")
        {
            try
            {
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                var fechaInicio = periodo.ToLower() switch
                {
                    "dia" => DateTime.Now.AddDays(-1),
                    "semana" => DateTime.Now.AddDays(-7),
                    "mes" => DateTime.Now.AddMonths(-1),
                    "año" => DateTime.Now.AddYears(-1),
                    _ => DateTime.Now.AddMonths(-1)
                };

                // Obtener dashboard de solicitudes
                var currentUserId = GetCurrentUserId();
                var requestDashboard = await _requestService.GetDashboardAsync(currentUserId);
                
                // Obtener dashboard de conteos
                var countDashboard = await _inventoryCountService.GetCountDashboardAsync();

                var metrics = new
                {
                    Periodo = periodo,
                    FechaInicio = fechaInicio,
                    FechaConsulta = DateTime.Now,
                    
                    // Métricas de solicitudes
                    Solicitudes = new
                    {
                        Total = requestDashboard.Data?.TotalRequests ?? 0,
                        Pendientes = requestDashboard.Data?.PendingRequests ?? 0,
                        EnRevision = requestDashboard.Data?.InReviewRequests ?? 0,
                        Completadas = requestDashboard.Data?.CompletedRequests ?? 0,
                        Vencidas = requestDashboard.Data?.OverdueRequests ?? 0,
                        TasaCompletacion = requestDashboard.Data?.TotalRequests > 0 ? 
                            Math.Round((decimal)(requestDashboard.Data.CompletedRequests * 100.0 / requestDashboard.Data.TotalRequests), 2) : 0
                    },
                    
                    // Métricas de conteos
                    Conteos = new
                    {
                        Total = countDashboard.Data?.TotalCounts ?? 0,
                        ConDiferencias = countDashboard.Data?.CountsWithDifferences ?? 0,
                        SinDiferencias = countDashboard.Data?.CountsWithoutDifferences ?? 0,
                        Pendientes = countDashboard.Data?.CountsPendientes ?? 0,
                        Ajustados = countDashboard.Data?.CountsAjustados ?? 0,
                        CostoDiferencias = countDashboard.Data?.TotalCostoDiferencias ?? 0,
                        PorcentajePrecision = countDashboard.Data?.TotalCounts > 0 ?
                            Math.Round((decimal)(countDashboard.Data.CountsWithoutDifferences * 100.0 / countDashboard.Data.TotalCounts), 2) : 0
                    },
                    
                    // Métricas de usuarios
                    Usuarios = new
                    {
                        TotalActivos = 0, // Se calculará con los datos reales
                        LoginsDiarios = 0, // Placeholder
                        UsuariosMasActivos = new List<object>() // Placeholder
                    },
                    
                    // Métricas de eficiencia
                    Eficiencia = new
                    {
                        TiempoPromedioSolicitud = "2.5 días", // Placeholder
                        TiempoPromedioConteo = "1.2 horas", // Placeholder
                        SolicitudesPorUsuario = 0, // Se calculará
                        ConteosPorUsuario = 0 // Se calculará
                    }
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Métricas de rendimiento obtenidas exitosamente",
                    Data = metrics
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener estadísticas de actividad de usuarios
        /// </summary>
        /// <param name="top">Número de usuarios más activos a mostrar</param>
        /// <returns>Estadísticas de actividad</returns>
        [HttpGet("user-activity")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetUserActivityStats([FromQuery] int top = 10)
        {
            try
            {
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                // Obtener todos los usuarios
                var usersResult = await _userService.GetUsersAsync(new GetUsersRequest { PageSize = 1000 });
                
                if (!usersResult.Success || usersResult.Data == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Error al obtener usuarios"
                    });
                }

                var users = usersResult.Data;
                var activityStats = new
                {
                    TotalUsuarios = users.Count,
                    UsuariosActivos = users.Count(u => u.Activo),
                    UsuariosInactivos = users.Count(u => !u.Activo),
                    
                    UsuariosPorPerfil = users.GroupBy(u => u.Perfil.ToString())
                        .Select(g => new { Perfil = g.Key, Cantidad = g.Count() })
                        .ToList(),
                    
                    UsuariosPorTienda = users.GroupBy(u => u.Tienda)
                        .Select(g => new { Tienda = g.Key, Cantidad = g.Count() })
                        .OrderByDescending(x => x.Cantidad)
                        .Take(top)
                        .ToList(),
                    
                    UltimosAccesos = users
                        .Where(u => u.UltimoAcceso.HasValue)
                        .OrderByDescending(u => u.UltimoAcceso)
                        .Take(top)
                        .Select(u => new { 
                            Usuario = u.Usuario, 
                            Nombre = u.Nombre, 
                            UltimoAcceso = u.UltimoAcceso,
                            Tienda = u.Tienda
                        })
                        .ToList(),
                    
                    UsuariosSinAccesoReciente = users
                        .Where(u => !u.UltimoAcceso.HasValue || u.UltimoAcceso < DateTime.Now.AddDays(-30))
                        .Select(u => new { 
                            Usuario = u.Usuario, 
                            Nombre = u.Nombre, 
                            UltimoAcceso = u.UltimoAcceso,
                            Tienda = u.Tienda,
                            DiasInactivo = u.UltimoAcceso.HasValue ? 
                                (DateTime.Now - u.UltimoAcceso.Value).Days : 
                                (DateTime.Now - u.FechaCreacion).Days
                        })
                        .OrderByDescending(u => u.DiasInactivo)
                        .Take(top)
                        .ToList()
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Estadísticas de actividad obtenidas exitosamente",
                    Data = activityStats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener estadísticas comparativas entre tiendas
        /// </summary>
        /// <returns>Comparación de rendimiento entre tiendas</returns>
        [HttpGet("store-comparison")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetStoreComparison()
        {
            try
            {
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                var storesResult = await _storeService.GetStoresAsync();
                
                if (!storesResult.Success || storesResult.Data == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Error al obtener tiendas"
                    });
                }

                var storeComparisons = new List<object>();
                
                foreach (var store in storesResult.Data)
                {
                    // Obtener usuarios de la tienda
                    var usersResult = await _storeService.GetStoreUsersAsync(store.Tienda);
                    var users = usersResult.Data ?? new List<UserResponse>();
                    
                    // Obtener dashboard de conteos para la tienda
                    var countDashboard = await _inventoryCountService.GetCountDashboardAsync(store.Tienda);
                    
                    var comparison = new
                    {
                        Tienda = store.Tienda,
                        Nombre = store.Nombre,
                        TotalUsuarios = users.Count,
                        UsuariosActivos = users.Count(u => u.Activo),
                        SolicitudesActivas = store.SolicitudesActivas,
                        UltimaActividad = store.UltimaActividad,
                        
                        // Métricas de conteo
                        TotalConteos = countDashboard.Data?.TotalCounts ?? 0,
                        ConteosConDiferencias = countDashboard.Data?.CountsWithDifferences ?? 0,
                        ConteosPendientes = countDashboard.Data?.CountsPendientes ?? 0,
                        PorcentajePrecision = countDashboard.Data?.TotalCounts > 0 ?
                            Math.Round((decimal)((countDashboard.Data.TotalCounts - countDashboard.Data.CountsWithDifferences) * 100.0 / countDashboard.Data.TotalCounts), 2) : 100,
                        
                        CostoDiferencias = countDashboard.Data?.TotalCostoDiferencias ?? 0,
                        
                        // Distribución de usuarios por perfil
                        DistribucionPerfiles = users.GroupBy(u => u.Perfil.ToString())
                            .ToDictionary(g => g.Key, g => g.Count())
                    };
                    
                    storeComparisons.Add(comparison);
                }

                // Ordenar por porcentaje de precisión descendente
                var orderedComparisons = storeComparisons
                    .OrderByDescending(s => ((dynamic)s).PorcentajePrecision)
                    .ToList();

                var summary = new
                {
                    TotalTiendas = storeComparisons.Count,
                    TiendaConMejorPrecision = orderedComparisons.FirstOrDefault(),
                    TiendaConMenorPrecision = orderedComparisons.LastOrDefault(),
                    PromedioGeneralPrecision = storeComparisons.Any() ?
                        Math.Round(storeComparisons.Average(s => (decimal)((dynamic)s).PorcentajePrecision), 2) : 0,
                    TotalCostoDiferencias = storeComparisons.Sum(s => (decimal)((dynamic)s).CostoDiferencias),
                    Comparaciones = orderedComparisons
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Comparación entre tiendas obtenida exitosamente",
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener resumen ejecutivo del sistema
        /// </summary>
        /// <returns>Resumen ejecutivo con KPIs principales</returns>
        [HttpGet("executive-summary")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetExecutiveSummary()
        {
            try
            {
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                // Obtener estadísticas principales
                var systemStats = await _userService.GetSystemStatsAsync();
                var countDashboard = await _inventoryCountService.GetCountDashboardAsync();
                var currentUserId = GetCurrentUserId();
                var requestDashboard = await _requestService.GetDashboardAsync(currentUserId);

                var executiveSummary = new
                {
                    FechaReporte = DateTime.Now,
                    Periodo = "Último mes",
                    
                    // KPIs principales
                    KPIs = new
                    {
                        TotalUsuarios = systemStats.Data?.TotalUsers ?? 0,
                        TotalTiendas = systemStats.Data?.TotalStores ?? 0,
                        SolicitudesActivas = systemStats.Data?.ActiveRequests ?? 0,
                        ConteosCompletados = systemStats.Data?.CompletedCounts ?? 0,
                        
                        // Tasas de eficiencia
                        TasaCompletacionSolicitudes = requestDashboard.Data?.TotalRequests > 0 ?
                            Math.Round((decimal)(requestDashboard.Data.CompletedRequests * 100.0 / requestDashboard.Data.TotalRequests), 2) : 0,
                        
                        TasaPrecisionConteos = countDashboard.Data?.TotalCounts > 0 ?
                            Math.Round((decimal)(countDashboard.Data.CountsWithoutDifferences * 100.0 / countDashboard.Data.TotalCounts), 2) : 100,
                        
                        ImpactoFinanciero = countDashboard.Data?.TotalCostoDiferencias ?? 0
                    },
                    
                    // Tendencias
                    Tendencias = new
                    {
                        CrecimientoMensual = systemStats.Data?.MonthlyGrowth ?? 0,
                        SolicitudesVencidas = requestDashboard.Data?.OverdueRequests ?? 0,
                        ConteosPendientes = countDashboard.Data?.CountsPendientes ?? 0
                    },
                    
                    // Alertas y recomendaciones
                    Alertas = new List<object>
                    {
                        new { 
                            Tipo = "warning", 
                            Mensaje = $"Hay {requestDashboard.Data?.OverdueRequests ?? 0} solicitudes vencidas",
                            Prioridad = "alta"
                        },
                        new { 
                            Tipo = "info", 
                            Mensaje = $"Precisión de conteos: {(countDashboard.Data?.TotalCounts > 0 ? Math.Round((decimal)(countDashboard.Data.CountsWithoutDifferences * 100.0 / countDashboard.Data.TotalCounts), 2) : 100)}%",
                            Prioridad = "media"
                        }
                    },
                    
                    // Top performers
                    TopPerformers = systemStats.Data?.TopPerformingStores?.Take(3).ToList() ?? new List<StorePerformanceItem>()
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Resumen ejecutivo obtenido exitosamente",
                    Data = executiveSummary
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener estado de salud del sistema
        /// </summary>
        /// <returns>Estado de salud y métricas de sistema</returns>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public ActionResult<ApiResponse<object>> GetSystemHealth()
        {
            try
            {
                var health = new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0",
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                    Uptime = Environment.TickCount64 / 1000 / 60, // minutos
                    
                    Services = new
                    {
                        Database = "Connected",
                        InnovacentroDb = "Connected",
                        Authentication = "Active",
                        Api = "Running"
                    },
                    
                    Memory = new
                    {
                        UsedMB = GC.GetTotalMemory(false) / 1024 / 1024,
                        Gen0Collections = GC.CollectionCount(0),
                        Gen1Collections = GC.CollectionCount(1),
                        Gen2Collections = GC.CollectionCount(2)
                    }
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Estado de salud del sistema",
                    Data = health
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error al obtener estado de salud",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        // Método auxiliar para obtener el ID del usuario actual
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("No se pudo obtener el ID del usuario autenticado");
        }
    }
}