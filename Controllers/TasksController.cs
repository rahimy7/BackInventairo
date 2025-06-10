using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventarioAPI.Services;
using InventarioAPI.Models.Tasks;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [ApiExplorerSettings(GroupName = "Tareas")]

    public class TasksController : ControllerBase
    {
        private readonly ITaskService _taskService;

        public TasksController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        /// <summary>
        /// Crea una nueva tarea.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] TaskRequest request)
        {
            var result = await _taskService.CreateTaskAsync(request);
            return Ok(result);
        }

     [HttpGet]
public async Task<IActionResult> GetTasks([FromQuery] int? assignedToId, [FromQuery] bool? completed)
{
    var result = await _taskService.GetTasksAsync(assignedToId, completed);
    return Ok(result);
}


        /// <summary>
        /// Marca una tarea como completada.
        /// </summary>
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteTask(int id)
        {
            var result = await _taskService.CompleteTaskAsync(id);
            return Ok(result);
        }

        /// <summary>
        /// Elimina lógicamente una tarea.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var result = await _taskService.DeleteTaskAsync(id);
            return Ok(result);
        }

        /// <summary>
        /// Obtiene estadísticas generales de las tareas.
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var result = await _taskService.GetStatsAsync();
            return Ok(result);
        }
    }
}
