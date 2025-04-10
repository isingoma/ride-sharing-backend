using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Ride_Sharing_Backend.src.MatchmakingService.Hub;
using Ride_Sharing_Backend.src.Shared.Constants;
using Ride_Sharing_Backend.src.Shared.Models;
using StackExchange.Redis;
using System.Text.Json;
using IDatabase = StackExchange.Redis.IDatabase;

namespace Ride_Sharing_Backend.src.MatchmakingService.Controllers
{
    [ApiController]
    [Route("api")]
    public class RideMatchController : ControllerBase
    {
        private readonly IDatabase _redis;
        private readonly HttpClient _httpClient;
        private readonly RideHub _hubContext;
        private readonly string _googleMapsApiKey;

        public RideMatchController(IConnectionMultiplexer redis, HttpClient httpClient, IConfiguration configuration)
        {
            _redis = redis.GetDatabase();
            _httpClient = httpClient;
            _hubContext = new RideHub();
            _googleMapsApiKey = configuration["AppSettings:GoogleMapsApiKey"]; // Fetch Google Maps API Key from appsettings.json
        }
        [HttpPost("request-ride")]
        public async Task<IActionResult> RequestRide([FromBody] RideRequest request)
        {
            // Step 1: Cache rider location if available
            var cachedLocation = await _redis.StringGetAsync($"driver:{request.RiderId}:location");
            if (!cachedLocation.IsNullOrEmpty)
            {
                var location = JsonSerializer.Deserialize<UserLocation>(cachedLocation);
                request.PickupLatitude = location.Latitude;
                request.PickupLongitude = location.Longitude;
            }
            else
            {
                var googleMapsUrl = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={request.PickupLatitude},{request.PickupLongitude}&key={_googleMapsApiKey}";
                var response = await _httpClient.GetStringAsync(googleMapsUrl);
                await _redis.StringSetAsync($"driver:{request.RiderId}:location", response, TimeSpan.FromMinutes(5));
            }

            // Step 2: Search for nearby drivers
            var drivers = await _redis.GeoRadiusAsync(
                RedisKeys.DriverGeoSet,
                request.PickupLongitude,
                request.PickupLatitude,
                radius: 5000,
                unit: GeoUnit.Meters,
                count: 5,
                order: Order.Ascending);

            if (drivers.Length == 0)
                return NotFound("No nearby drivers available.");

            var selectedDriver = drivers.First();
            var rideId = Guid.NewGuid().ToString();

            // Step 3: Dynamic Pricing Logic
            var regionId = $"{Math.Round(request.PickupLatitude, 1)}:{Math.Round(request.PickupLongitude, 1)}";
            await _redis.StringIncrementAsync($"demand:{regionId}");
            await _redis.StringIncrementAsync($"supply:{regionId}");

            int demand = (int)(await _redis.StringGetAsync($"demand:{regionId}"));
            int supply = (int)(await _redis.StringGetAsync($"supply:{regionId}"));

            decimal surgeMultiplier = GetSurgeMultiplier(demand, supply);

            decimal baseFare = 5000; // UGX or any other currency
            decimal finalFare = baseFare * surgeMultiplier;

            var rideStatus = new RideStatus
            {
                RideId = rideId,
                RiderId = request.RiderId,
                DriverId = selectedDriver.Member.ToString(),
                Status = "Accepted"
            };

            await _redis.HashSetAsync(RedisKeys.RideStatusHash, rideId, JsonSerializer.Serialize(rideStatus));

            // Step 4: Notify clients via WebSocket
            await _hubContext.Clients.All.SendAsync("ReceiveRideUpdate", rideId, rideStatus.Status);

            // Step 5: Return final result including fare and surge multiplier
            return Ok(new
            {
                RideId = rideId,
                DriverId = selectedDriver.Member.ToString(),
                Fare = finalFare,
                SurgeMultiplier = surgeMultiplier
            });
        }

        // Surge multiplier logic
        private decimal GetSurgeMultiplier(int demand, int supply)
        {
            if (supply == 0) return 2.0m;
            var ratio = (decimal)demand / supply;

            if (ratio < 1.0m) return 1.0m;
            if (ratio < 2.0m) return 1.2m;
            if (ratio < 3.0m) return 1.5m;

            return 2.0m;
        }


        [HttpGet("ride-status/{id}")]
        public async Task<IActionResult> GetRideStatus(string id)
        {
            var result = await _redis.HashGetAsync(RedisKeys.RideStatusHash, id);
            if (result.IsNullOrEmpty) return NotFound();
            return Ok(JsonSerializer.Deserialize<RideStatus>(result!));
        }

        [HttpGet("drivers")]
        public async Task<IActionResult> GetDrivers()
        {
            var drivers = await _redis.SortedSetRangeByRankAsync(RedisKeys.DriverGeoSet);
            return Ok(drivers.Select(d => d.ToString()).ToList());
        }

    }
}
