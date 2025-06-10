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
        [HttpPost("create-from-request")]
        [ProducesResponseType(typeof(ApiResponse<List<int>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
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

                return result.Success ? Ok(result) : BadRequest(result);
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
        [HttpPut("register-physical-count")]
        [ProducesResponseType(typeof(ApiResponse<InventoryCountResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<InventoryCountResponse>>> RegisterPhysicalCount([FromBody] RegisterPhysicalCountRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var result = await _inventoryCountService.RegisterPhysicalCountAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Actualizar el estado de un conteo
        /// </summary>
        [HttpPut("update-status")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateCountStatus([FromBody] UpdateCountStatusRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var result = await _inventoryCountService.UpdateCountStatusAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Obtener conteos de inventario con filtros y paginación
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<InventoryCountResponse>>>> GetInventoryCounts(
            [FromQuery] GetInventoryCountsRequest request)
        {
            request.PageSize = Math.Min(request.PageSize, 100);
            var result = await _inventoryCountService.GetInventoryCountsAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Obtener un conteo por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<InventoryCountResponse>>> GetInventoryCountById(int id)
        {
            var result = await _inventoryCountService.GetInventoryCountByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>
        /// Obtener historial de un conteo
        /// </summary>
        [HttpGet("{countId}/history")]
        public async Task<ActionResult<ApiResponse<List<InventoryCountHistoryResponse>>>> GetCountHistory(int countId)
        {
            var result = await _inventoryCountService.GetCountHistoryAsync(countId);
            return Ok(result);
        }

        /// <summary>
        /// Obtener dashboard con estadísticas
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<ApiResponse<InventoryCountDashboardResponse>>> GetCountDashboard(
            [FromQuery] string tienda = "")
        {
            var result = await _inventoryCountService.GetCountDashboardAsync(tienda);
            return Ok(result);
        }

        /// <summary>
        /// Obtener conteos asignados al usuario actual
        /// </summary>
        [HttpGet("my-counts")]
        public async Task<ActionResult<ApiResponse<List<InventoryCountSummaryResponse>>>> GetMyAssignedCounts()
        {
            var currentUserId = GetCurrentUserId();
            var result = await _inventoryCountService.GetMyAssignedCountsAsync(currentUserId);
            return Ok(result);
        }

        /// <summary>
        /// Agregar comentario a un conteo
        /// </summary>
        [HttpPost("comment")]
        public async Task<ActionResult<ApiResponse<bool>>> AddCountComment([FromBody] AddCountCommentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var result = await _inventoryCountService.AddCountCommentAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Actualizar múltiples conteos
        /// </summary>
        [HttpPut("batch-update")]
        public async Task<ActionResult<ApiResponse<object>>> BatchUpdateCounts([FromBody] BatchUpdateCountsRequest request)
        {
            if (!ModelState.IsValid || !request.Counts.Any())
                return BadRequest("Datos inválidos o lista vacía");

            var currentUserId = GetCurrentUserId();
            var result = await _inventoryCountService.BatchUpdateCountsAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Obtener conteos pendientes por solicitud
        /// </summary>
        [HttpGet("pending/request/{requestId}")]
        public async Task<ActionResult<ApiResponse<List<InventoryCountResponse>>>> GetPendingCountsByRequest(int requestId)
        {
            var result = await _inventoryCountService.GetPendingCountsByRequestAsync(requestId);
            return Ok(result);
        }

        /// <summary>
        /// Obtener conteos con diferencias por tienda
        /// </summary>
        [HttpGet("differences/tienda/{tienda}")]
        public async Task<ActionResult<ApiResponse<PagedResponse<InventoryCountResponse>>>> GetCountsWithDifferencesByTienda(
            string tienda,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
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

        /// <summary>
        /// Obtener estadísticas por división
        /// </summary>
        [HttpGet("stats/division/{divisionCode}")]
        public async Task<ActionResult<ApiResponse<object>>> GetDivisionStats(string divisionCode, [FromQuery] string tienda = "")
        {
            var request = new GetInventoryCountsRequest
            {
                DivisionCode = divisionCode,
                Tienda = tienda,
                PageSize = 1000
            };

            var result = await _inventoryCountService.GetInventoryCountsAsync(request);
            
            if (!result.Success || result.Data == null)
                return Ok(new ApiResponse<object> { Success = true, Message = "No hay conteos disponibles", Data = new { TotalCounts = 0 } });

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
                CountsByStatus = counts.GroupBy(c => c.Estado).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                CountsByMovementType = counts.Where(c => c.IsPhysicalCountRegistered)
                    .GroupBy(c => c.TipoMovimiento)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count())
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Estadísticas obtenidas exitosamente",
                Data = stats
            });
        }

        /// <summary>
        /// Obtener resumen de conteos por usuario
        /// </summary>
        [HttpGet("summary/user/{userId?}")]
        public async Task<ActionResult<ApiResponse<object>>> GetUserCountSummary(int? userId = null)
        {
            var targetUserId = userId ?? GetCurrentUserId();
            var result = await _inventoryCountService.GetMyAssignedCountsAsync(targetUserId);
            
            if (!result.Success || result.Data == null)
                return Ok(new ApiResponse<object> { Success = true, Message = "No hay conteos asignados", Data = new { TotalAssignedCounts = 0 } });

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
                CountsByStatus = counts.GroupBy(c => c.Estado).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                CountsByTienda = counts.GroupBy(c => c.Tienda).ToDictionary(g => g.Key, g => g.Count())
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Resumen obtenido exitosamente",
                Data = summary
            });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            throw new UnauthorizedAccessException("No se pudo obtener el ID del usuario");
        }
    }
}