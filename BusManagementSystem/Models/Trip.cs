
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusManagementSystem.Models
{
    public class Trip
    {
        [Key]
        public int TripID { get; set; }

        [Required]
        public int BusID { get; set; }

        [Required]
        public int DriverID { get; set; }

        [Required]
        public int FromCampusID { get; set; }

        [Required]
        public int ToCampusID { get; set; }

        [Required]
        [Display(Name = "Thời gian khởi hành")]
        public DateTime DepartureTime { get; set; }

        [Display(Name = "Thời gian đến dự kiến")]
        public DateTime? EstimatedArrivalTime { get; set; }

        [Display(Name = "Thời gian đến thực tế")]
        public DateTime? ActualArrivalTime { get; set; }

        [StringLength(50)]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Chờ xác nhận";

        [Display(Name = "Số chỗ còn trống")]
        public int AvailableSeats { get; set; } = 31;

        public bool IsApprovedByAdmin { get; set; } = false;

        public int? ApprovedByAdminID { get; set; }

        public DateTime? ApprovedDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("BusID")]
        public virtual Bus? Bus { get; set; }

        [ForeignKey("DriverID")]
        public virtual User? Driver { get; set; }

        [ForeignKey("FromCampusID")]
        public virtual Campus? FromCampus { get; set; }

        [ForeignKey("ToCampusID")]
        public virtual Campus? ToCampus { get; set; }

        [ForeignKey("ApprovedByAdminID")]
        public virtual User? ApprovedByAdmin { get; set; }

        public virtual ICollection<Booking>? Bookings { get; set; }
        public virtual ICollection<TripStatusHistory>? StatusHistories { get; set; }
    }
}
