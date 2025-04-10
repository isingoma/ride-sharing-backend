namespace Ride_Sharing_Backend.src.Shared.Models
{
    public class UserLocation
    {
        public string UserId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
