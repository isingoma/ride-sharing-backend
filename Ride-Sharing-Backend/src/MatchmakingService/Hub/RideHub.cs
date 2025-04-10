using Microsoft.AspNetCore.SignalR;

namespace Ride_Sharing_Backend.src.MatchmakingService.Hub
{
    public class RideHub : DynamicHub
    {
        // Clients can receive real-time updates of ride status
        public async Task SendRideUpdate(string rideId, string status)
        {
            await Clients.All.SendAsync("ReceiveRideUpdate", rideId, status);
        }
    }
}
