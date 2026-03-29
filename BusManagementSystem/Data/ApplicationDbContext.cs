
using Microsoft.EntityFrameworkCore;
using BusManagementSystem.Models;


namespace BusManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Campus> Campuses { get; set; }
        public DbSet<Bus> Buses { get; set; }
        public DbSet<Trip> Trips { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<TripStatusHistory> TripStatusHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<Trip>()
                .HasOne(t => t.FromCampus)
                .WithMany(c => c.TripsFrom)
                .HasForeignKey(t => t.FromCampusID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Trip>()
                .HasOne(t => t.ToCampus)
                .WithMany(c => c.TripsTo)
                .HasForeignKey(t => t.ToCampusID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Trip>()
                .HasOne(t => t.Driver)
                .WithMany(u => u.TripsAsDriver)
                .HasForeignKey(t => t.DriverID)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for performance
            modelBuilder.Entity<Trip>()
                .HasIndex(t => t.Status);

            modelBuilder.Entity<Trip>()
                .HasIndex(t => t.DepartureTime);

            modelBuilder.Entity<Bus>()
                .HasIndex(b => b.BusNumber)
                .IsUnique();

            modelBuilder.Entity<Bus>()
                .HasIndex(b => b.LicensePlate)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
        }
    }
}
