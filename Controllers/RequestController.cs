using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using InventarioAPI.Models;
using InventarioAPI.Services;
using System.Security.Claims;
using InventarioAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService _requestService;
        private readonly InventarioDbContext _context;
        private readonly InnovacentroDbContext _innovacentroContext;

        public RequestController(IRequestService requestService, InventarioDbContext context, InnovacentroDbContext innovacentroContext)
        {
            _requestService = requestService;
            _context = context;
            _innovacentroContext = innovacentroContext;
        }

        /// <summary>
        /// Crear una nueva solicitud de códigos
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<RequestResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<RequestResponse>>> CreateRequest([FromBody] CreateRequestRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var result = await _requestService.CreateRequestAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Obtener solicitudes con filtros y paginación
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<RequestResponse>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestResponse>>>> GetRequests(
            [FromQuery] GetRequestsRequest request)
        {
            request.PageSize = Math.Min(request.PageSize, 100);
            var result = await _requestService.GetRequestsAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Obtener solicitud por ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<RequestResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<RequestResponse>>> GetRequestById(int id)
        {
            var result = await _requestService.GetRequestByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>
        /// Obtener solicitud por ticket
        /// </summary>
        [HttpGet("ticket/{ticketNumber}")]
        public async Task<ActionResult<ApiResponse<RequestResponse>>> GetRequestByTicket(string ticketNumber)
        {
            var result = await _requestService.GetRequestByTicketAsync(ticketNumber);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>
        /// Actualizar estado de código
        /// </summary>
        [HttpPut("code/status")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateCodeStatus([FromBody] UpdateCodeStatusRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var result = await _requestService.UpdateCodeStatusAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Asignar código a usuario
        /// </summary>
        [HttpPut("code/assign")]
        public async Task<ActionResult<ApiResponse<bool>>> AssignCode([FromBody] AssignCodeRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var result = await _requestService.AssignCodeAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Agregar comentario
        /// </summary>
        [HttpPost("comment")]
        public async Task<ActionResult<ApiResponse<bool>>> AddComment([FromBody] AddCommentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var result = await _requestService.AddCommentAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Obtener historial de solicitud
        /// </summary>
        [HttpGet("{requestId}/history")]
        public async Task<ActionResult<ApiResponse<List<RequestHistoryResponse>>>> GetRequestHistory(int requestId)
        {
            var result = await _requestService.GetRequestHistoryAsync(requestId);
            return Ok(result);
        }

        /// <summary>
        /// Obtener dashboard
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<ApiResponse<RequestDashboardResponse>>> GetDashboard([FromQuery] string tienda = "")
        {
            var currentUserId = GetCurrentUserId();
            var result = await _requestService.GetDashboardAsync(currentUserId, tienda);
            return Ok(result);
        }

        /// <summary>
        /// Obtener códigos asignados al usuario
        /// </summary>
        [HttpGet("my-codes")]
        public async Task<ActionResult<ApiResponse<List<RequestCodeResponse>>>> GetMyAssignedCodes()
        {
            var currentUserId = GetCurrentUserId();
            var result = await _requestService.GetMyAssignedCodesAsync(currentUserId);
            return Ok(result);
        }

        /// <summary>
        /// Obtener solicitudes del usuario
        /// </summary>
        [HttpGet("my-requests")]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestResponse>>>> GetMyRequests(
            [FromQuery] RequestStatus? status = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var currentUserId = GetCurrentUserId();
            var request = new GetRequestsRequest
            {
                RequestorID = currentUserId,
                Status = status,
                PageNumber = pageNumber,
                PageSize = Math.Min(pageSize, 100)
            };

            var result = await _requestService.GetRequestsAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Actualizar múltiples códigos
        /// </summary>
        [HttpPut("codes/batch-update")]
        public async Task<ActionResult<ApiResponse<object>>> BatchUpdateCodes([FromBody] List<UpdateCodeStatusRequest> requests)
        {
            if (!ModelState.IsValid || !requests.Any())
                return BadRequest("Datos inválidos o lista vacía");

            var currentUserId = GetCurrentUserId();
            var results = new List<object>();
            var successCount = 0;
            var failCount = 0;

            foreach (var request in requests)
            {
                var result = await _requestService.UpdateCodeStatusAsync(request, currentUserId);
                results.Add(new
                {
                    CodeID = request.CodeID,
                    Success = result.Success,
                    Message = result.Message
                });

                if (result.Success)
                    successCount++;
                else
                    failCount++;
            }

            return Ok(new ApiResponse<object>
            {
                Success = failCount == 0,
                Message = $"Procesadas {requests.Count} actualizaciones. Exitosas: {successCount}, Fallidas: {failCount}",
                Data = new { Results = results, SuccessCount = successCount, FailCount = failCount }
            });
        }

        /// <summary>
        /// Estadísticas por tienda
        /// </summary>
        [HttpGet("stats/tienda/{tienda}")]
        public async Task<ActionResult<ApiResponse<object>>> GetTiendaStats(string tienda)
        {
            var currentUserId = GetCurrentUserId();
            var dashboard = await _requestService.GetDashboardAsync(currentUserId, tienda);

            if (!dashboard.Success)
                return BadRequest(dashboard);

            var stats = new
            {
                Tienda = tienda,
                TotalRequests = dashboard.Data.TotalRequests,
                PendingRequests = dashboard.Data.PendingRequests,
                InReviewRequests = dashboard.Data.InReviewRequests,
                CompletedRequests = dashboard.Data.CompletedRequests,
                OverdueRequests = dashboard.Data.OverdueRequests,
                CompletionRate = dashboard.Data.TotalRequests > 0 ?
                    Math.Round((decimal)dashboard.Data.CompletedRequests / dashboard.Data.TotalRequests * 100, 2) : 0
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Estadísticas obtenidas exitosamente",
                Data = stats
            });
        }

        /// <summary>
        /// Obtener todas las solicitudes (admin)
        /// </summary>
        [HttpGet("admin/all")]
        [Authorize(Roles = "ADMINISTRADOR")]
        public async Task<IActionResult> GetAllAdminRequests()
        {
            var result = await _requestService.GetAllAdminRequestsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Actividad reciente
        /// </summary>
        [HttpGet("recent-activity")]
        public async Task<IActionResult> GetRecentActivity([FromQuery] int count = 20)
        {
            var result = await _requestService.GetRecentActivityAsync(count);
            return Ok(result);
        }

        /// <summary>
        /// Crear solicitudes en lote
        /// </summary>
        [HttpPost("bulk-create")]
        public async Task<ActionResult<ApiResponse<List<RequestResponse>>>> BulkCreateRequests([FromBody] BulkCreateRequest request)
        {
            if (!ModelState.IsValid || request.Requests == null || !request.Requests.Any())
                return BadRequest("Datos inválidos o lista vacía");

            var currentUserId = GetCurrentUserId();
            var result = await _requestService.BulkCreateRequestsAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Asignar códigos en lote
        /// </summary>
        [HttpPut("bulk-assign")]
        public async Task<IActionResult> BulkAssign([FromBody] BulkAssignCodesRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _requestService.BulkAssignCodesAsync(request, userId);
            return Ok(result);
        }

        /// <summary>
        /// Actualizar estados en lote
        /// </summary>
        [HttpPut("bulk-update-status")]
        public async Task<IActionResult> BulkStatus([FromBody] BulkUpdateStatusRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _requestService.BulkUpdateStatusAsync(request, userId);
            return Ok(result);
        }

        /// <summary>
        /// Filtrar por divisiones
        /// </summary>
        [HttpPost("by-divisions")]
        public async Task<IActionResult> GetByDivisions([FromBody] DivisionFilterRequest request)
        {
            var result = await _requestService.GetRequestsByDivisionsAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Cerrar solicitud
        /// </summary>
        [HttpPut("{requestId}/close")]
        public async Task<IActionResult> CloseRequest(int requestId)
        {
            var userId = GetCurrentUserId();
            var result = await _requestService.CloseRequestAsync(requestId, userId);
            return Ok(result);
        }

        /// <summary>
        /// Obtener producto por código
        /// </summary>
        [HttpGet("product/{productCode}")]
        public async Task<IActionResult> GetProductByCode(string productCode)
        {
            try
            {
                var product = await _innovacentroContext.ProductViews
                    .Where(p => p.Code == productCode)
                    .FirstOrDefaultAsync();

                if (product == null)
                    return Ok(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Producto no encontrado"
                    });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = product
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error al buscar producto",
                    Errors = new List<string> { ex.Message }
                });
            }
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