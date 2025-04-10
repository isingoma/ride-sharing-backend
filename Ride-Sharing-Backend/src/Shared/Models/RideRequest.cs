namespace Ride_Sharing_Backend.src.Shared.Models
{
    public class RideRequest
    {
        public string RiderId { get; set; } = string.Empty;
        public double PickupLatitude { get; set; }
        public double PickupLongitude { get; set; }
    }
}
