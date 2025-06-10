using System;

namespace InventarioAPI.Models.Tasks
{
    public class TaskResponse
    {
        public int ID { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int AssignedTo { get; set; }
    }
}
