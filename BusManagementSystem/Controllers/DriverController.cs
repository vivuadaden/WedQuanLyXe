using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BusManagementSystem.Data;
using BusManagementSystem.Models;
using BusManagementSystem.Services;
using System.Security.Claims;

namespace BusManagementSystem.Controllers
{
    [Authorize(Roles = "Driver")]
    public class DriverController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IRealtimeService _realtimeService;

        public DriverController(ApplicationDbContext context, IRealtimeService realtimeService)
        {
            _context = context;
            _realtimeService = realtimeService;
        }

        private int GetCurrentDriverId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        // Dashboard
        public async Task<IActionResult> Index()
        {
            var driverId = GetCurrentDriverId();

            var stats = new
            {
                TotalTrips = await _context.Trips.CountAsync(t => t.DriverID == driverId),
                PendingTrips = await _context.Trips.CountAsync(t => t.DriverID == driverId && t.Status == "Chờ xác nhận"),
                ActiveTrips = await _context.Trips.CountAsync(t => t.DriverID == driverId && t.Status == "Đang đi"),
                CompletedTrips = await _context.Trips.CountAsync(t => t.DriverID == driverId && t.Status == "Hoàn thành")
            };

            ViewBag.Stats = stats;

            var currentTrip = await _context.Trips
                .Include(t => t.Bus)
                .Include(t => t.FromCampus)
                .Include(t => t.ToCampus)
                .FirstOrDefaultAsync(t => t.DriverID == driverId &&
                    (t.Status == "Đã xác nhận" || t.Status == "Đang đi"));

            return View(currentTrip);
        }

        // My Trips
        public async Task<IActionResult> MyTrips()
        {
            var driverId = GetCurrentDriverId();

            var trips = await _context.Trips
                .Include(t => t.Bus)
                .Include(t => t.FromCampus)
                .Include(t => t.ToCampus)
                .Where(t => t.DriverID == driverId)
                .OrderByDescending(t => t.DepartureTime)
                .ToListAsync();

            return View(trips);
        }

        // Register New Trip
        [HttpGet]
        public async Task<IActionResult> RegisterTrip()
        {
            // Get available buses
            var availableBuses = await _context.Buses
                .Where(b => b.IsActive && b.Status == "Sẵn sàng")
                .ToListAsync();

            // Get all campuses
            var campuses = await _context.Campuses
                .Where(c => c.IsActive)
                .ToListAsync();

            ViewBag.AvailableBuses = availableBuses;
            ViewBag.Campuses = campuses;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterTrip(Trip model)
        {
            try
            {
                var driverId = GetCurrentDriverId();

                // Check if bus is available
                var busInUse = await _context.Trips
                    .AnyAsync(t => t.BusID == model.BusID &&
                        (t.Status == "Đã xác nhận" || t.Status == "Đang đi"));

                if (busInUse)
                {
                    TempData["Error"] = "Xe này đang được sử dụng cho chuyến khác!";
                    return RedirectToAction(nameof(RegisterTrip));
                }

                // Check if driver has active trip
                var driverHasActiveTrip = await _context.Trips
                    .AnyAsync(t => t.DriverID == driverId &&
                        (t.Status == "Chờ xác nhận" || t.Status == "Đã xác nhận" || t.Status == "Đang đi"));

                if (driverHasActiveTrip)
                {
                    TempData["Error"] = "Bạn đã có chuyến đang hoạt động!";
                    return RedirectToAction(nameof(RegisterTrip));
                }

                var trip = new Trip
                {
                    BusID = model.BusID,
                    DriverID = driverId,
                    FromCampusID = model.FromCampusID,
                    ToCampusID = model.ToCampusID,
                    DepartureTime = model.DepartureTime,
                    EstimatedArrivalTime = model.EstimatedArrivalTime,
                    Status = "Chờ xác nhận",
                    AvailableSeats = 31,
                    IsApprovedByAdmin = false,
                    CreatedDate = DateTime.Now
                };

                _context.Trips.Add(trip);
                await _context.SaveChangesAsync();

                // Notify admins about new trip
                await _realtimeService.NotifyNewTripRegistered(trip.TripID);

                TempData["Success"] = "Đăng ký chuyến đi thành công! Vui lòng chờ admin xác nhận.";
                return RedirectToAction(nameof(MyTrips));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction(nameof(RegisterTrip));
            }
        }

        // Start Trip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartTrip(int id)
        {
            try
            {
                var driverId = GetCurrentDriverId();
                var trip = await _context.Trips
                    .Include(t => t.Bus)
                    .FirstOrDefaultAsync(t => t.TripID == id && t.DriverID == driverId);

                if (trip == null)
                {
                    return NotFound();
                }

                if (trip.Status != "Đã xác nhận")
                {
                    TempData["Error"] = "Chuyến đi chưa được xác nhận!";
                    return RedirectToAction(nameof(MyTrips));
                }

                trip.Status = "Đang đi";
                await _context.SaveChangesAsync();

                // Notify students
                await _realtimeService.NotifyTripStatusChanged(trip.TripID, "Đang đi");

                TempData["Success"] = "Đã bắt đầu chuyến đi!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // Complete Trip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteTrip(int id)
        {
            try
            {
                var driverId = GetCurrentDriverId();
                var trip = await _context.Trips
                    .Include(t => t.Bus)
                    .FirstOrDefaultAsync(t => t.TripID == id && t.DriverID == driverId);

                if (trip == null)
                {
                    return NotFound();
                }

                trip.Status = "Hoàn thành";
                trip.ActualArrivalTime = DateTime.Now;

                if (trip.Bus != null)
                {
                    trip.Bus.Status = "Sẵn sàng";
                }

                await _context.SaveChangesAsync();

                // Notify students
                await _realtimeService.NotifyTripStatusChanged(trip.TripID, "Hoàn thành");

                TempData["Success"] = "Đã hoàn thành chuyến đi!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // View Trip Details & Bookings
        [HttpGet]
        public async Task<IActionResult> TripDetails(int id)
        {
            var driverId = GetCurrentDriverId();

            var trip = await _context.Trips
                .Include(t => t.Bus)
                .Include(t => t.FromCampus)
                .Include(t => t.ToCampus)
                .FirstOrDefaultAsync(t => t.TripID == id && t.DriverID == driverId);

            if (trip == null)
            {
                return NotFound();
            }

            var bookings = await _context.Bookings
                .Include(b => b.Student)
                .Where(b => b.TripID == id && b.Status == "Đã đặt")
                .OrderBy(b => b.BookingTime)
                .ToListAsync();

            ViewBag.Bookings = bookings;

            return View(trip);
        }

        // Cancel Trip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTrip(int id)
        {
            try
            {
                var driverId = GetCurrentDriverId();
                var trip = await _context.Trips
                    .Include(t => t.Bus)
                    .FirstOrDefaultAsync(t => t.TripID == id && t.DriverID == driverId);

                if (trip == null)
                {
                    return NotFound();
                }

                if (trip.Status == "Đang đi" || trip.Status == "Hoàn thành")
                {
                    TempData["Error"] = "Không thể hủy chuyến đi đang hoạt động hoặc đã hoàn thành!";
                    return RedirectToAction(nameof(MyTrips));
                }

                trip.Status = "Hủy";

                if (trip.Bus != null)
                {
                    trip.Bus.Status = "Sẵn sàng";
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã hủy chuyến đi!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction(nameof(MyTrips));
        }
    }
}