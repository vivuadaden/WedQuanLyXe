
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusManagementSystem.Models
{
    public class TripStatusHistory
    {
        [Key]
        public int HistoryID { get; set; }

        [Required]
        public int TripID { get; set; }

        [StringLength(50)]
        public string? OldStatus { get; set; }

        [Required]
        [StringLength(50)]
        public string NewStatus { get; set; } = string.Empty;

        [Required]
        public int ChangedByUserID { get; set; }

        public DateTime ChangedDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey("TripID")]
        public virtual Trip? Trip { get; set; }

        [ForeignKey("ChangedByUserID")]
        public virtual User? ChangedByUser { get; set; }
    }
}
