using Microsoft.AspNetCore.Mvc;
using BusManagementSystem.Services;
using BusManagementSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace BusManagementSystem.Controllers
{
    public class TestPasswordController : Controller
    {
        private readonly IPasswordHasher _passwordHasher;
        private readonly ApplicationDbContext _context;

        public TestPasswordController(IPasswordHasher passwordHasher, ApplicationDbContext context)
        {
            _passwordHasher = passwordHasher;
            _context = context;
        }

        // Test tạo hash mới: /TestPassword/GenerateHash?password=Admin@123
        public IActionResult GenerateHash(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return Content("Vui lòng truyền ?password=your_password");
            }

            var hash = _passwordHasher.HashPassword(password);
            return Content($"Password: {password}\n\nHash: {hash}\n\nCopy hash này vào SQL để update admin!");
        }

        // Test verify password: /TestPassword/VerifyPassword?username=admin&password=Admin@123
        public async Task<IActionResult> VerifyPassword(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return Content("Vui lòng truyền ?username=admin&password=Admin@123");
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                return Content($"❌ User '{username}' không tồn tại trong database!");
            }

            var isValid = _passwordHasher.VerifyPassword(password, user.PasswordHash);

            var result = $@"
=== THÔNG TIN USER ===
Username: {user.Username}
FullName: {user.FullName}
Email: {user.Email}
Role: {user.Role?.RoleName ?? "NULL"}
IsActive: {user.IsActive}

=== KIỂM TRA PASSWORD ===
Password bạn nhập: {password}
Password Hash trong DB: {user.PasswordHash}

Kết quả: {(isValid ? "✅ PASSWORD ĐÚNG!" : "❌ PASSWORD SAI!")}

{(isValid ? "Bạn có thể đăng nhập được!" : "Password không khớp, hãy tạo hash mới và update vào DB!")}
";

            return Content(result);
        }

        // Xem tất cả users: /TestPassword/ListUsers
        public async Task<IActionResult> ListUsers()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Select(u => new
                {
                    u.UserID,
                    u.Username,
                    u.FullName,
                    Role = u.Role != null ? u.Role.RoleName : "NULL",
                    u.IsActive
                })
                .ToListAsync();

            var result = "=== DANH SÁCH USERS ===\n\n";
            foreach (var user in users)
            {
                result += $"ID: {user.UserID} | Username: {user.Username} | Name: {user.FullName} | Role: {user.Role} | Active: {user.IsActive}\n";
            }

            return Content(result);
        }

        // Reset admin password: /TestPassword/ResetAdmin
        public async Task<IActionResult> ResetAdmin()
        {
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin");

            if (admin == null)
            {
                return Content("❌ Admin không tồn tại! Hãy tạo user admin trước trong SQL.");
            }

            // Hash password mới: Admin@123
            admin.PasswordHash = _passwordHasher.HashPassword("Admin@123");
            await _context.SaveChangesAsync();

            return Content(@"
✅ ĐÃ RESET PASSWORD ADMIN THÀNH CÔNG!

Username: admin
Password: Admin@123

Bây giờ bạn có thể đăng nhập!
");
        }
    }
}