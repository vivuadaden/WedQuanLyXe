
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusManagementSystem.Models
{
    public class Bus
    {
        [Key]
        public int BusID { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Số xe")]
        public string BusNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "Biển số")]
        public string LicensePlate { get; set; } = string.Empty;

        [Display(Name = "Sức chứa")]
        public int Capacity { get; set; } = 31;

        [StringLength(50)]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Sẵn sàng";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public virtual ICollection<Trip>? Trips { get; set; }
    }
}
