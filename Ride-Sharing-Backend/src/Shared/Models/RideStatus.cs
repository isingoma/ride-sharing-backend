namespace Ride_Sharing_Backend.src.Shared.Models
{
    public class RideStatus
    {
        public string RideId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Accepted, En Route, Completed
        public string DriverId { get; set; } = string.Empty;
        public string RiderId { get; set; } = string.Empty;
    }
}
