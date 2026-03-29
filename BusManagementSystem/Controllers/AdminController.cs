using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BusManagementSystem.Data;
using BusManagementSystem.Models;
using BusManagementSystem.Services;
using System.Security.Claims;

namespace BusManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IRealtimeService _realtimeService;
        private readonly IPasswordHasher _passwordHasher;

        public AdminController(ApplicationDbContext context, IRealtimeService realtimeService, IPasswordHasher passwordHasher)
        {
            _context = context;
            _realtimeService = realtimeService;
            _passwordHasher = passwordHasher;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        // ============ DASHBOARD ============
        public async Task<IActionResult> Index()
        {
            var stats = new
            {
                TotalDrivers = await _context.Users.CountAsync(u => u.RoleID == 2 && u.IsActive),
                TotalStudents = await _context.Users.CountAsync(u => u.RoleID == 3 && u.IsActive),
                TotalBuses = await _context.Buses.CountAsync(b => b.IsActive),
                TotalTrips = await _context.Trips.CountAsync(),
                PendingTrips = await _context.Trips.CountAsync(t => t.Status == "Chờ xác nhận"),
                ActiveTrips = await _context.Trips.CountAsync(t => t.Status == "Đang đi"),
                CompletedTrips = await _context.Trips.CountAsync(t => t.Status == "Hoàn thành"),
                TodayTrips = await _context.Trips.CountAsync(t => t.DepartureTime.Date == DateTime.Today),
                TotalBookings = await _context.Bookings.CountAsync()
            };

            ViewBag.Stats = stats;

            var pendingTrips = await _context.Trips
                .Include(t => t.Bus)
                .Include(t => t.Driver)
                .Include(t => t.FromCampus)
                .Include(t => t.ToCampus)
                .Where(t => t.Status == "Chờ xác nhận")
                .OrderBy(t => t.DepartureTime)
                .ToListAsync();

            return View(pendingTrips);
        }

        // ============ QUẢN LÝ TÀI XẾ ============

        public async Task<IActionResult> Drivers()
        {
            var drivers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.RoleID == 2)
                .OrderByDescending(u => u.CreatedDate)
                .ToListAsync();

            return View(drivers);
        }

        [HttpGet]
        public IActionResult CreateDriver()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDriver(User model)
        {
            try
            {
                if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                {
                    TempData["Error"] = "Tên đăng nhập đã tồn tại!";
                    return View(model);
                }

                var user = new User
                {
                    Username = model.Username,
                    PasswordHash = _passwordHasher.HashPassword(model.PasswordHash),
                    FullName = model.FullName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    RoleID = 2,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm tài xế thành công!";
                return RedirectToAction(nameof(Drivers));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditDriver(int id)
        {
            var driver = await _context.Users.FindAsync(id);
            if (driver == null || driver.RoleID != 2)
            {
                return NotFound();
            }
            return View(driver);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDriver(int id, User model)
        {
            try
            {
                var driver = await _context.Users.FindAsync(id);
                if (driver == null || driver.RoleID != 2)
                {
                    return NotFound();
                }

                driver.FullName = model.FullName;
                driver.Email = model.Email;
                driver.PhoneNumber = model.PhoneNumber;
                driver.IsActive = model.IsActive;

                if (!string.IsNullOrEmpty(model.PasswordHash))
                {
                    driver.PasswordHash = _passwordHasher.HashPassword(model.PasswordHash);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật thông tin tài xế thành công!";
                return RedirectToAction(nameof(Drivers));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDriver(int id)
        {
            try
            {
                var driver = await _context.Users.FindAsync(id);
                if (driver == null || driver.RoleID != 2)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài xế!" });
                }

                var hasActiveTrips = await _context.Trips
                    .AnyAsync(t => t.DriverID == id &&
                                  (t.Status == "Đã xác nhận" || t.Status == "Đang đi"));

                if (hasActiveTrips)
                {
                    return Json(new { success = false, message = "Không thể xóa tài xế đang có chuyến đi hoạt động!" });
                }

                driver.IsActive = false;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa tài xế thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // ============ QUẢN LÝ XE ============

        public async Task<IActionResult> Buses()
        {
            var buses = await _context.Buses
                .OrderByDescending(b => b.CreatedDate)
                .ToListAsync();

            return View(buses);
        }

        [HttpGet]
        public IActionResult CreateBus()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBus(Bus model)
        {
            try
            {
                if (await _context.Buses.AnyAsync(b => b.BusNumber == model.BusNumber))
                {
                    TempData["Error"] = "Số xe đã tồn tại!";
                    return View(model);
                }

                if (await _context.Buses.AnyAsync(b => b.LicensePlate == model.LicensePlate))
                {
                    TempData["Error"] = "Biển số xe đã tồn tại!";
                    return View(model);
                }

                var bus = new Bus
                {
                    BusNumber = model.BusNumber,
                    LicensePlate = model.LicensePlate,
                    Capacity = 31,
                    Status = "Sẵn sàng",
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.Buses.Add(bus);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm xe thành công!";
                return RedirectToAction(nameof(Buses));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditBus(int id)
        {
            var bus = await _context.Buses.FindAsync(id);
            if (bus == null)
            {
                return NotFound();
            }
            return View(bus);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBus(int id, Bus model)
        {
            if (id != model.BusID)
            {
                return NotFound();
            }

            try
            {
                var bus = await _context.Buses.FindAsync(id);
                if (bus == null)
                {
                    return NotFound();
                }

                if (await _context.Buses.AnyAsync(b => b.LicensePlate == model.LicensePlate && b.BusID != id))
                {
                    TempData["Error"] = "Biển số xe đã tồn tại!";
                    return View(model);
                }

                bus.LicensePlate = model.LicensePlate;
                bus.Status = model.Status;
                bus.IsActive = model.IsActive;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật xe thành công!";
                return RedirectToAction(nameof(Buses));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBus(int id)
        {
            try
            {
                var bus = await _context.Buses.FindAsync(id);
                if (bus == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy xe!" });
                }

                var hasActiveTrips = await _context.Trips
                    .AnyAsync(t => t.BusID == id &&
                                  (t.Status == "Đã xác nhận" || t.Status == "Đang đi"));

                if (hasActiveTrips)
                {
                    return Json(new { success = false, message = "Không thể xóa xe đang có chuyến đi hoạt động!" });
                }

                bus.IsActive = false;
                bus.Status = "Ngừng hoạt động";
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Vô hiệu hóa xe thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // ============ QUẢN LÝ CHUYẾN ĐI ============

        public async Task<IActionResult> Trips(string status = "all")
        {
            var query = _context.Trips
                .Include(t => t.Bus)
                .Include(t => t.Driver)
                .Include(t => t.FromCampus)
                .Include(t => t.ToCampus)
                .AsQueryable();

            if (status != "all")
            {
                query = query.Where(t => t.Status == status);
            }

            var trips = await query
                .OrderByDescending(t => t.DepartureTime)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            return View(trips);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTrip(int tripId)
        {
            try
            {
                var adminId = GetCurrentUserId();

                // Sử dụng AsNoTracking để tránh conflict
                var trip = await _context.Trips
                    .Include(t => t.Bus)
                    .FirstOrDefaultAsync(t => t.TripID == tripId);

                if (trip == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy chuyến đi!" });
                }

                if (trip.Status != "Chờ xác nhận")
                {
                    return Json(new { success = false, message = "Chuyến đi đã được xử lý!" });
                }

                trip.Status = "Đã xác nhận";
                trip.IsApprovedByAdmin = true;
                trip.ApprovedByAdminID = adminId;
                trip.ApprovedDate = DateTime.Now;

                if (trip.Bus != null)
                {
                    trip.Bus.Status = "Đang hoạt động";
                }

                // Ghi lịch sử
                var history = new TripStatusHistory
                {
                    TripID = tripId,
                    OldStatus = "Chờ xác nhận",
                    NewStatus = "Đã xác nhận",
                    ChangedByUserID = adminId,
                    Notes = "Admin xác nhận chuyến đi",
                    ChangedDate = DateTime.Now
                };
                _context.TripStatusHistories.Add(history);

                await _context.SaveChangesAsync();

                // Gửi thông báo realtime
                await _realtimeService.NotifyTripApproved(tripId);

                return Json(new { success = true, message = "Xác nhận chuyến đi thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTrip(int tripId, string reason)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var trip = await _context.Trips
                    .Include(t => t.Bus)
                    .FirstOrDefaultAsync(t => t.TripID == tripId);

                if (trip == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy chuyến đi!" });
                }

                var oldStatus = trip.Status;
                trip.Status = "Hủy";

                if (trip.Bus != null)
                {
                    trip.Bus.Status = "Sẵn sàng";
                }

                // Ghi lịch sử
                var history = new TripStatusHistory
                {
                    TripID = tripId,
                    OldStatus = oldStatus,
                    NewStatus = "Hủy",
                    ChangedByUserID = adminId,
                    Notes = $"Admin từ chối: {reason}",
                    ChangedDate = DateTime.Now
                };
                _context.TripStatusHistories.Add(history);

                await _context.SaveChangesAsync();

                // Gửi thông báo realtime
                await _realtimeService.NotifyTripRejected(tripId, reason);

                return Json(new { success = true, message = "Từ chối chuyến đi thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // Chi tiết chuyến đi - SỬA TÊN: TripDetail (không có 's')
        public async Task<IActionResult> TripDetails(int id)
        {
            var trip = await _context.Trips
                .Include(t => t.Bus)
                .Include(t => t.Driver)
                .Include(t => t.FromCampus)
                .Include(t => t.ToCampus)
                .Include(t => t.ApprovedByAdmin)
                .Include(t => t.Bookings)
                    .ThenInclude(b => b.Student)
                .Include(t => t.StatusHistories)
                    .ThenInclude(h => h.ChangedByUser)
                .FirstOrDefaultAsync(t => t.TripID == id);

            if (trip == null)
            {
                return NotFound();
            }

            return View(trip);
        }
    }
}