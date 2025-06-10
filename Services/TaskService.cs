using InventarioAPI.Data;
using InventarioAPI.Models;
using InventarioAPI.Models.Tasks;
using Microsoft.EntityFrameworkCore;

namespace InventarioAPI.Services
{
    public class TaskService : ITaskService
    {
        private readonly InventarioDbContext _context;

        public TaskService(InventarioDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<int>> CreateTaskAsync(TaskRequest request)
        {
            var task = new TaskEntity
            {
                Title = request.Title,
                Description = request.Description,
                AssignedTo = request.AssignedTo
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return new ApiResponse<int>
            {
                Success = true,
                Message = "Tarea creada exitosamente",
                Data = task.ID
            };
        }

public async Task<ApiResponse<List<TaskResponse>>> GetTasksAsync(int? assignedToId = null, bool? IsCompleted = null)
{
    var query = _context.Tasks.Where(t => t.IsActive);

    if (assignedToId.HasValue)
        query = query.Where(t => t.AssignedTo == assignedToId.Value);

    if (IsCompleted.HasValue)
        query = query.Where(t => t.IsCompleted == IsCompleted.Value);

    var tasks = await query
        .OrderByDescending(t => t.CreatedDate)
        .Select(t => new TaskResponse
        {
         ID = t.Id,
            Title = t.Title,
            Description = t.Description,
            DueDate = t.DueDate,
            AssignedTo = t.AssignedTo,
            IsCompleted = t.IsCompleted,
            CreatedDate = t.CreatedDate
        }).ToListAsync();

    return new ApiResponse<List<TaskResponse>>
    {
        Success = true,
        Message = "Tareas obtenidas exitosamente",
        Data = tasks
    };
}


        public async Task<ApiResponse<TaskStatsResponse>> GetStatsAsync()
        {
            var total = await _context.Tasks.CountAsync(t => t.IsActive);
            var completed = await _context.Tasks.CountAsync(t => t.IsActive && t.IsCompleted);
            var pending = total - completed;

            return new ApiResponse<TaskStatsResponse>
            {
                Success = true,
                Data = new TaskStatsResponse
                {
                    TotalTasks = total,
                    CompletedTasks = completed,
                    PendingTasks = pending
                }
            };
        }

        public async Task<ApiResponse<bool>> CompleteTaskAsync(int taskId)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.ID == taskId && t.IsActive);
            if (task == null)
            {
                return new ApiResponse<bool> { Success = false, Message = "Tarea no encontrada" };
            }

            task.IsCompleted = true;
            task.CompletedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new ApiResponse<bool> { Success = true, Message = "Tarea completada", Data = true };
        }

        public async Task<ApiResponse<bool>> DeleteTaskAsync(int taskId)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.ID == taskId && t.IsActive);
            if (task == null)
            {
                return new ApiResponse<bool> { Success = false, Message = "Tarea no encontrada" };
            }

            task.IsActive = false;
            await _context.SaveChangesAsync();

            return new ApiResponse<bool> { Success = true, Message = "Tarea eliminada", Data = true };
        }
    }
}
