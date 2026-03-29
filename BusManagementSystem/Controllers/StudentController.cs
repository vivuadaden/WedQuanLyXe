using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BusManagementSystem.Data;
using BusManagementSystem.Models;
using BusManagementSystem.Services;
using System.Security.Claims;

namespace BusManagementSystem.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IRealtimeService _realtimeService;

        public StudentController(ApplicationDbContext context, IRealtimeService realtimeService)
        {
            _context = context;
            _realtimeService = realtimeService;
        }

        private int GetCurrentStudentId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        // Available Trips
        public async Task<IActionResult> Index(string? fromCampus, string? toCampus)
        {
            var query = _context.Trips
                .Include(t => t.Bus)
                .Include(t => t.Driver)
                .Include(t => t.FromCampus)
                .Include(t => t.ToCampus)
                .Where(t => (t.Status == "Đã xác nhận" || t.Status == "Đang đi") &&
                           t.AvailableSeats > 0)
                .AsQueryable();

            // Filter by campus
            if (!string.IsNullOrEmpty(fromCampus))
            {
                query = query.Where(t => t.FromCampus.CampusName.Contains(fromCampus));
            }

            if (!string.IsNullOrEmpty(toCampus))
            {
                query = query.Where(t => t.ToCampus.CampusName.Contains(toCampus));
            }

            var trips = await query
                .OrderBy(t => t.DepartureTime)
                .ToListAsync();

            // Get campuses for filter
            var campuses = await _context.Campuses
                .Where(c => c.IsActive)
                .ToListAsync();

            ViewBag.Campuses = campuses;
            ViewBag.FromCampus = fromCampus;
            ViewBag.ToCampus = toCampus;

            return View(trips);
        }

        // Book Seat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookSeat(int tripId)
        {
            try
            {
                var studentId = GetCurrentStudentId();

                var trip = await _context.Trips.FindAsync(tripId);
                if (trip == null)
                {
                    return NotFound();
                }

                // Check if already booked
                var existingBooking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.TripID == tripId &&
                                            b.StudentID == studentId &&
                                            b.Status == "Đã đặt");

                if (existingBooking != null)
                {
                    TempData["Error"] = "Bạn đã đặt chỗ cho chuyến đi này rồi!";
                    return RedirectToAction(nameof(Index));
                }

                // Check available seats
                if (trip.AvailableSeats <= 0)
                {
                    TempData["Error"] = "Chuyến đi đã hết chỗ!";
                    return RedirectToAction(nameof(Index));
                }

                // Create booking
                var booking = new Booking
                {
                    TripID = tripId,
                    StudentID = studentId,
                    BookingTime = DateTime.Now,
                    Status = "Đã đặt"
                };

                _context.Bookings.Add(booking);

                // Decrease available seats
                trip.AvailableSeats--;

                await _context.SaveChangesAsync();

                // Notify all students about seat update
                await _realtimeService.NotifySeatBooking(tripId, trip.AvailableSeats);

                TempData["Success"] = "Đặt chỗ thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // My Bookings
        public async Task<IActionResult> MyBookings()
        {
            var studentId = GetCurrentStudentId();

            var bookings = await _context.Bookings
                .Include(b => b.Trip)
                    .ThenInclude(t => t.Bus)
                .Include(b => b.Trip)
                    .ThenInclude(t => t.Driver)
                .Include(b => b.Trip)
                    .ThenInclude(t => t.FromCampus)
                .Include(b => b.Trip)
                    .ThenInclude(t => t.ToCampus)
                .Where(b => b.StudentID == studentId)
                .OrderByDescending(b => b.BookingTime)
                .ToListAsync();

            return View(bookings);
        }

        // Cancel Booking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int id)
        {
            try
            {
                var studentId = GetCurrentStudentId();

                var booking = await _context.Bookings
                    .Include(b => b.Trip)
                    .FirstOrDefaultAsync(b => b.BookingID == id && b.StudentID == studentId);

                if (booking == null)
                {
                    return NotFound();
                }

                if (booking.Status != "Đã đặt")
                {
                    TempData["Error"] = "Không thể hủy đặt chỗ này!";
                    return RedirectToAction(nameof(MyBookings));
                }

                // Check if trip is still active
                if (booking.Trip.Status == "Hoàn thành" || booking.Trip.Status == "Hủy")
                {
                    TempData["Error"] = "Chuyến đi đã kết thúc, không thể hủy!";
                    return RedirectToAction(nameof(MyBookings));
                }

                // Update booking status
                booking.Status = "Đã hủy";

                // Increase available seats
                booking.Trip.AvailableSeats++;

                await _context.SaveChangesAsync();

                // Notify all students about seat update
                await _realtimeService.NotifySeatBooking(booking.TripID, booking.Trip.AvailableSeats);

                TempData["Success"] = "Đã hủy đặt chỗ!";    
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction(nameof(MyBookings));
        }

        // Trip Details
        [HttpGet]
        public async Task<IActionResult> TripDetails(int id)
        {
            var trip = await _context.Trips
                .Include(t => t.Bus)
                .Include(t => t.Driver)
                .Include(t => t.FromCampus)
                .Include(t => t.ToCampus)
                .FirstOrDefaultAsync(t => t.TripID == id);

            if (trip == null)
            {
                return NotFound();
            }

            var studentId = GetCurrentStudentId();

            // Check if student has booked this trip
            var hasBooked = await _context.Bookings
                .AnyAsync(b => b.TripID == id &&
                             b.StudentID == studentId &&
                             b.Status == "Đã đặt");

            ViewBag.HasBooked = hasBooked;

            return View(trip);
        }
    }
}