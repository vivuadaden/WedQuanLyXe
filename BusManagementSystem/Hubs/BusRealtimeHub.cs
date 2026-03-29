using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using BusManagementSystem.Hubs;

namespace BusManagementSystem.Hubs
{
    [Authorize]
    public class BusRealtimeHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userRole = Context.User?.FindFirst("Role")?.Value;

            if (!string.IsNullOrEmpty(userRole))
            {
                // Thêm user vào group theo role
                await Groups.AddToGroupAsync(Context.ConnectionId, userRole);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userRole = Context.User?.FindFirst("Role")?.Value;

            if (!string.IsNullOrEmpty(userRole))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userRole);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Driver join trip group để nhận cập nhật realtime
        public async Task JoinTripGroup(int tripId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Trip_{tripId}");
        }

        // Driver leave trip group
        public async Task LeaveTripGroup(int tripId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Trip_{tripId}");
        }
    }
}

// Service để gửi thông báo realtime
namespace BusManagementSystem.Services
{
    public interface IRealtimeService
    {
        Task NotifyNewTripRegistered(int tripId);
        Task NotifyTripApproved(int tripId);
        Task NotifyTripRejected(int tripId, string reason);
        Task NotifyTripStatusChanged(int tripId, string newStatus);
        Task NotifySeatBooking(int tripId, int availableSeats);
    }

    public class RealtimeService : IRealtimeService
    {
        private readonly IHubContext<BusRealtimeHub> _hubContext;

        public RealtimeService(IHubContext<BusRealtimeHub> hubContext)
        {
            _hubContext = hubContext;
        }

        // Thông báo cho admin có chuyến đi mới chờ xác nhận
        public async Task NotifyNewTripRegistered(int tripId)
        {
            await _hubContext.Clients.Group("Admin").SendAsync("NewTripRegistered", new
            {
                tripId = tripId,
                message = "Có chuyến đi mới chờ xác nhận!",
                timestamp = DateTime.Now
            });
        }

        // Thông báo cho driver chuyến đi được xác nhận
        public async Task NotifyTripApproved(int tripId)
        {
            await _hubContext.Clients.Group($"Trip_{tripId}").SendAsync("TripApproved", new
            {
                tripId = tripId,
                message = "Chuyến đi của bạn đã được xác nhận!",
                timestamp = DateTime.Now
            });

            // Thông báo cho sinh viên có chuyến mới
            await _hubContext.Clients.Group("Student").SendAsync("NewTripAvailable", new
            {
                tripId = tripId,
                message = "Có chuyến đi mới!",
                timestamp = DateTime.Now
            });
        }

        // Thông báo cho driver chuyến đi bị từ chối
        public async Task NotifyTripRejected(int tripId, string reason)
        {
            await _hubContext.Clients.Group($"Trip_{tripId}").SendAsync("TripRejected", new
            {
                tripId = tripId,
                reason = reason,
                message = "Chuyến đi của bạn bị từ chối!",
                timestamp = DateTime.Now
            });
        }

        // Thông báo thay đổi trạng thái chuyến đi
        public async Task NotifyTripStatusChanged(int tripId, string newStatus)
        {
            // Gửi cho tất cả user đang theo dõi chuyến đi này
            await _hubContext.Clients.Group($"Trip_{tripId}").SendAsync("TripStatusChanged", new
            {
                tripId = tripId,
                status = newStatus,
                timestamp = DateTime.Now
            });

            // Gửi cho sinh viên
            await _hubContext.Clients.Group("Student").SendAsync("TripStatusChanged", new
            {
                tripId = tripId,
                status = newStatus,
                timestamp = DateTime.Now
            });

            // Gửi cho admin
            await _hubContext.Clients.Group("Admin").SendAsync("TripStatusChanged", new
            {
                tripId = tripId,
                status = newStatus,
                timestamp = DateTime.Now
            });
        }

        // Thông báo khi có người đặt/hủy chỗ
        public async Task NotifySeatBooking(int tripId, int availableSeats)
        {
            // Cập nhật số chỗ cho tất cả user đang xem chuyến đi
            await _hubContext.Clients.All.SendAsync("SeatAvailabilityChanged", new
            {
                tripId = tripId,
                availableSeats = availableSeats,
                timestamp = DateTime.Now
            });
        }
    }
}

// Helper để hash password
namespace BusManagementSystem.Services
{
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
    }

    public class PasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}