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
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryCountService _inventoryCountService;
        private readonly IRequestService _requestService;

        public InventoryController(IInventoryCountService inventoryCountService, IRequestService requestService)
        {
            _inventoryCountService = inventoryCountService;
            _requestService = requestService;
        }

        /// <summary>
        /// Obtener dashboard del usuario de inventario
        /// </summary>
        [HttpGet("dashboard/{tienda}")]
        public async Task<IActionResult> GetDashboard(string tienda)
        {
            var result = await _inventoryCountService.GetCountDashboardAsync(tienda);
            return Ok(result);
        }

        /// <summary>
        /// Obtener conteos pendientes de validación
        /// </summary>
        [HttpGet("counts-validation")]
        public async Task<IActionResult> GetCountsToValidate([FromQuery] string tienda = "", [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var request = new GetInventoryCountsRequest
            {
                Tienda = tienda,
                Estado = CountStatus.EN_REVISION,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _inventoryCountService.GetInventoryCountsAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Validar un conteo
        /// </summary>
        [HttpPut("validate-count")]
        public async Task<IActionResult> ValidateCount([FromBody] UpdateCountStatusRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _inventoryCountService.UpdateCountStatusAsync(request, userId);
            return Ok(result);
        }

        /// <summary>
        /// Obtener solicitudes listas para cerrar
        /// </summary>
        [HttpGet("requests-to-close/{tienda}")]
        public async Task<IActionResult> GetRequestsToClose(string tienda)
        {
            // Obtener solicitudes donde todos los códigos estén procesados
            var requestsQuery = new GetRequestsRequest
            {
                Tienda = tienda,
                Status = RequestStatus.EN_REVISION,
                PageSize = 100
            };

            var requestsResult = await _requestService.GetRequestsAsync(requestsQuery);
            
            if (!requestsResult.Success || requestsResult.Data?.Data == null)
                return Ok(new ApiResponse<List<RequestResponse>>
                {
                    Success = false,
                    Message = "No se pudieron obtener las solicitudes"
                });

            // Filtrar solo las solicitudes donde todos los códigos están listos
            var requestsToClose = new List<RequestResponse>();
            
            foreach (var request in requestsResult.Data.Data)
            {
                // Obtener detalles completos de la solicitud con códigos
                var detailResult = await _requestService.GetRequestByIdAsync(request.ID);
                
                if (detailResult.Success && detailResult.Data != null)
                {
                    var allCodesReady = detailResult.Data.Codes.All(c => 
                        c.Status == RequestStatus.LISTO || 
                        c.Status == RequestStatus.AJUSTADO);
                    
                    if (allCodesReady && detailResult.Data.Codes.Any())
                    {
                        requestsToClose.Add(detailResult.Data);
                    }
                }
            }

            return Ok(new ApiResponse<List<RequestResponse>>
            {
                Success = true,
                Message = $"Se encontraron {requestsToClose.Count} solicitudes listas para cerrar",
                Data = requestsToClose
            });
        }

        /// <summary>
        /// Cerrar una solicitud completada
        /// </summary>
        [HttpPut("close-request")]
        public async Task<IActionResult> CloseRequest([FromBody] int requestId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _requestService.CloseRequestAsync(requestId, userId);
            return Ok(result);
        }
    }
}