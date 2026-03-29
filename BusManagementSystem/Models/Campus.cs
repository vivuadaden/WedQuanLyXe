
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusManagementSystem.Models
{
    public class Campus
    {
        [Key]
        public int CampusID { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Tên cơ sở")]
        public string CampusName { get; set; } = string.Empty;

        [StringLength(255)]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Trip>? TripsFrom { get; set; }
        public virtual ICollection<Trip>? TripsTo { get; set; }
    }
}
