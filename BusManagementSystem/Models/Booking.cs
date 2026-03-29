
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BusManagementSystem.Models
{
    public class Booking
    {
        [Key]
        public int BookingID { get; set; }

        [Required]
        public int TripID { get; set; }

        [Required]
        public int StudentID { get; set; }

        public DateTime BookingTime { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string Status { get; set; } = "Đã đặt";

        // Navigation properties
        [ForeignKey("TripID")]
        public virtual Trip? Trip { get; set; }

        [ForeignKey("StudentID")]
        public virtual User? Student { get; set; }
    }
}
