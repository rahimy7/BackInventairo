using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using InventarioAPI.Models;
using InventarioAPI.Services;
using System.Security.Claims;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class InventoryCountController : ControllerBase
    {
        private readonly IInventoryCountService _inventoryCountService;

        public InventoryCountController(IInventoryCountService inventoryCountService)
        {
            _inventoryCountService = inventoryCountService;
        }

        /// <summary>
        /// Crear conteos de inventario desde una solicitud existente
        /// </summary>
        /// <param name="request">Datos para crear los conteos</param>
        /// <returns>Lista de IDs de conteos creados</returns>
        [HttpPost("create-from-request")]
        [ProducesResponseType(typeof(ApiResponse<List<int>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<int>>>> CreateInventoryCounts([FromBody] CreateInventoryCountsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                var currentUserId = GetCurrentUserId();
                var result = await _inventoryCountService.CreateInventoryCountsAsync(request, currentUserId);

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
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
        /// Registrar el conteo físico de un producto
        /// </summary>
        /// <param name="request">Datos del conteo físico</param>
        /// <returns>Conteo actualizado</returns>
        [HttpPut("register-physical-count")]
        [ProducesResponseType(typeof(ApiResponse<InventoryCountResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<InventoryCountResponse>>> RegisterPhysicalCount([FromBody] RegisterPhysicalCountRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                var currentUserId = GetCurrentUserId();
                var result = await _inventoryCountService.RegisterPhysicalCountAsync(request, currentUserId);

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
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
        /// Actualizar el estado de un conteo
        /// </summary>
        /// <param name="request">Datos para actualizar el estado</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPut("update-status")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateCountStatus([FromBody] UpdateCountStatusRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                var currentUserId = GetCurrentUserId();
                var result = await _inventoryCountService.UpdateCountStatusAsync(request, currentUserId);

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
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
        /// Obtener conteos de inventario con filtros y paginación
        /// </summary>
        /// <param name="requestId">ID de solicitud (opcional)</param>
        /// <param name="tienda">Filtrar por tienda</param>
        /// <param name="estado">Filtrar por estado</param>
        /// <param name="estatusCodigoFilter">Filtrar por estatus de código</param>
        /// <param name="divisionCode">Filtrar por división</param>
        /// <param name="categoria">Filtrar por categoría</param>
        /// <param name="fromDate">Fecha desde</param>
        /// <param name="toDate">Fecha hasta</param>
        /// <param name="assignedToId">Filtrar por usuario asignado</param>
        /// <param name="hasDifferences">Filtrar por diferencias</param>
        /// <param name="pageNumber">Número de página</param>
        /// <param name="pageSize">Tamaño de página</param>
        /// <param name="searchTerm">Término de búsqueda</param>
        /// <returns>Lista paginada de conteos</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<InventoryCountResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResponse<InventoryCountResponse>>>> GetInventoryCounts(
            [FromQuery] int? requestId = null,
            [FromQuery] string tienda = "",
            [FromQuery] CountStatus? estado = null,
            [FromQuery] string estatusCodigoFilter = "",
            [FromQuery] string divisionCode = "",
            [FromQuery] string categoria = "",
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? assignedToId = null,
            [FromQuery] bool? hasDifferences = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string searchTerm = "")
        {
            try
            {
                var request = new GetInventoryCountsRequest
                {
                    RequestID = requestId,
                    Tienda = tienda,
                    Estado = estado,
                    EstatusCodigoFilter = estatusCodigoFilter,
                    DivisionCode = divisionCode,
                    Categoria = categoria,
                    FromDate = fromDate,
                    ToDate = toDate,
                    AssignedToID = assignedToId,
                    HasDifferences = hasDifferences,
                    PageNumber = pageNumber,
                    PageSize = Math.Min(pageSize, 100),
                    SearchTerm = searchTerm
                };

                var result = await _inventoryCountService.GetInventoryCountsAsync(request);
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
        /// Obtener un conteo de inventario por su ID
        /// </summary>
        /// <param name="id">ID del conteo</param>
        /// <returns>Conteo de inventario</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<InventoryCountResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<InventoryCountResponse>>> GetInventoryCountById(int id)
        {
            try
            {
                var result = await _inventoryCountService.GetInventoryCountByIdAsync(id);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return NotFound(result);
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
        /// Obtener el historial de un conteo
        /// </summary>
        /// <param name="countId">ID del conteo</param>
        /// <returns>Lista de eventos del historial</returns>
        [HttpGet("{countId}/history")]
        [ProducesResponseType(typeof(ApiResponse<List<InventoryCountHistoryResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<InventoryCountHistoryResponse>>>> GetCountHistory(int countId)
        {
            try
            {
                var result = await _inventoryCountService.GetCountHistoryAsync(countId);
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
        /// Obtener dashboard con estadísticas de conteos
        /// </summary>
        /// <param name="tienda">Filtrar por tienda (opcional)</param>
        /// <returns>Dashboard con estadísticas</returns>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(ApiResponse<InventoryCountDashboardResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<InventoryCountDashboardResponse>>> GetCountDashboard([FromQuery] string tienda = "")
        {
            try
            {
                var result = await _inventoryCountService.GetCountDashboardAsync(tienda);
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
        /// Obtener conteos asignados al usuario actual
        /// </summary>
        /// <returns>Lista de conteos asignados</returns>
        [HttpGet("my-counts")]
        [ProducesResponseType(typeof(ApiResponse<List<InventoryCountSummaryResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<InventoryCountSummaryResponse>>>> GetMyAssignedCounts()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = await _inventoryCountService.GetMyAssignedCountsAsync(currentUserId);
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
        /// Agregar comentario a un conteo
        /// </summary>
        /// <param name="request">Datos del comentario</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("comment")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> AddCountComment([FromBody] AddCountCommentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                var currentUserId = GetCurrentUserId();
                var result = await _inventoryCountService.AddCountCommentAsync(request, currentUserId);

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
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
        /// Actualizar múltiples conteos a la vez
        /// </summary>
        /// <param name="request">Lista de conteos a actualizar</param>
        /// <returns>Resultado de las operaciones</returns>
        [HttpPut("batch-update")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> BatchUpdateCounts([FromBody] BatchUpdateCountsRequest request)
        {
            try
            {
                if (!ModelState.IsValid || !request.Counts.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos o lista vacía"
                    });
                }

                var currentUserId = GetCurrentUserId();
                var result = await _inventoryCountService.BatchUpdateCountsAsync(request, currentUserId);

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
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
        /// Obtener conteos pendientes por solicitud
        /// </summary>
        /// <param name="requestId">ID de la solicitud</param>
        /// <returns>Lista de conteos pendientes</returns>
        [HttpGet("pending/request/{requestId}")]
        [ProducesResponseType(typeof(ApiResponse<List<InventoryCountResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<InventoryCountResponse>>>> GetPendingCountsByRequest(int requestId)
        {
            try
            {
                var result = await _inventoryCountService.GetPendingCountsByRequestAsync(requestId);
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
        /// Obtener conteos con diferencias por tienda
        /// </summary>
        /// <param name="tienda">Código de la tienda</param>
        /// <param name="pageNumber">Número de página</param>
        /// <param name="pageSize">Tamaño de página</param>
        /// <returns>Lista de conteos con diferencias</returns>
        [HttpGet("differences/tienda/{tienda}")]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<InventoryCountResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResponse<InventoryCountResponse>>>> GetCountsWithDifferencesByTienda(
            string tienda,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var request = new GetInventoryCountsRequest
                {
                    Tienda = tienda,
                    HasDifferences = true,
                    PageNumber = pageNumber,
                    PageSize = Math.Min(pageSize, 100)
                };

                var result = await _inventoryCountService.GetInventoryCountsAsync(request);
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
        /// Obtener estadísticas de conteos por división
        /// </summary>
        /// <param name="divisionCode">Código de división</param>
        /// <param name="tienda">Tienda (opcional)</param>
        /// <returns>Estadísticas de la división</returns>
        [HttpGet("stats/division/{divisionCode}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetDivisionStats(string divisionCode, [FromQuery] string tienda = "")
        {
            try
            {
                var request = new GetInventoryCountsRequest
                {
                    DivisionCode = divisionCode,
                    Tienda = tienda,
                    PageSize = 1000
                };

                var result = await _inventoryCountService.GetInventoryCountsAsync(request);
                
                if (result.Success && result.Data != null)
                {
                    var counts = result.Data.Data;
                    var stats = new
                    {
                        DivisionCode = divisionCode,
                        Tienda = tienda,
                        TotalCounts = counts.Count,
                        CountsWithDifferences = counts.Count(c => c.HasDifference),
                        CountsWithoutDifferences = counts.Count(c => !c.HasDifference),
                        PendingCounts = counts.Count(c => !c.IsPhysicalCountRegistered),
                        TotalCostoDiferencias = counts.Sum(c => c.CostoTotal),
                        AverageStockCalculado = counts.Any() ? counts.Average(c => c.StockCalculado) : 0,
                        CountsByStatus = counts.GroupBy(c => c.Estado)
                            .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                        CountsByMovementType = counts.Where(c => c.IsPhysicalCountRegistered)
                            .GroupBy(c => c.TipoMovimiento)
                            .ToDictionary(g => g.Key.ToString(), g => g.Count())
                    };

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Estadísticas de división obtenidas exitosamente",
                        Data = stats
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "No hay conteos disponibles para esta división",
                    Data = new { TotalCounts = 0 }
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
        /// Obtener resumen de conteos por usuario
        /// </summary>
        /// <param name="userId">ID del usuario (opcional, por defecto usuario actual)</param>
        /// <returns>Resumen de conteos del usuario</returns>
        [HttpGet("summary/user/{userId?}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetUserCountSummary(int? userId = null)
        {
            try
            {
                var targetUserId = userId ?? GetCurrentUserId();
                var result = await _inventoryCountService.GetMyAssignedCountsAsync(targetUserId);
                
                if (result.Success && result.Data != null)
                {
                    var counts = result.Data;
                    var summary = new
                    {
                        UserId = targetUserId,
                        TotalAssignedCounts = counts.Count,
                        CompletedCounts = counts.Count(c => c.IsPhysicalCountRegistered),
                        PendingCounts = counts.Count(c => !c.IsPhysicalCountRegistered),
                        CountsWithDifferences = counts.Count(c => c.HasDifference),
                        CountsWithoutDifferences = counts.Count(c => !c.HasDifference),
                        TotalCostoDiferencias = counts.Sum(c => c.CostoTotal),
                        CompletionPercentage = counts.Any() ? 
                            Math.Round((decimal)counts.Count(c => c.IsPhysicalCountRegistered) / counts.Count * 100, 2) : 0,
                        CountsByStatus = counts.GroupBy(c => c.Estado)
                            .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                        CountsByTienda = counts.GroupBy(c => c.Tienda)
                            .ToDictionary(g => g.Key, g => g.Count())
                    };

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Resumen de usuario obtenido exitosamente",
                        Data = summary
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "No hay conteos asignados al usuario",
                    Data = new { TotalAssignedCounts = 0 }
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