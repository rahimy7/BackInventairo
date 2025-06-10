using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioAPI.Models.Tasks
{
    [Table("Tasks")]
    public class TaskEntity
    {
        [Key]
        public int ID { get; set; }
        public int Id { get; internal set; }
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = "";

        [MaxLength(1000)]
        public string? Description { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedDate { get; set; }

        [Required]
        public int AssignedTo { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? DueDate { get; internal set; }
    }
}
