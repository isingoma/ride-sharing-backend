using Moq;
using Ride_Sharing_Backend.src.MatchmakingService.Controllers;
using Ride_Sharing_Backend.src.Shared.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Ride_Sharing_Backend.src.MatchmakingService.Hub;
using System.Text.Json;

namespace Ride_Sharing_Backend.Tests
{
    public class RideMatchControllerTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockConnectionMultiplexer;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<HttpClient> _mockHttpClient;
        private readonly Mock<RideHub> _mockHubContext;
        private readonly RideMatchController _controller;
        private readonly IConfiguration _configuration;

        public RideMatchControllerTests(IConfiguration configuration)
        {
            _mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockHttpClient = new Mock<HttpClient>();
            _mockHubContext = new Mock<RideHub>();

            _mockConnectionMultiplexer.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);

            _controller = new RideMatchController(_mockConnectionMultiplexer.Object, _mockHttpClient.Object, configuration);
            _configuration = configuration;
        }

        [Fact]
        public async Task RequestRide_ShouldReturnOk_WhenDriverIsAvailable()
        {
            // Arrange
            var request = new RideRequest
            {
                RiderId = "rider1",
                PickupLatitude = 0.0,
                PickupLongitude = 0.0
            };

            // Mock Redis calls
            _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<string>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)JsonSerializer.Serialize(new UserLocation { Latitude = 0.0, Longitude = 0.0 }));

            // Act
            var result = await _controller.RequestRide(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<Dictionary<string, string>>(okResult.Value);
            Assert.Equal("driver1", returnValue["DriverId"]);
        }

        [Fact]
        public async Task RequestRide_ShouldReturnNotFound_WhenNoDriversAvailable()
        {
            // Arrange
            var request = new RideRequest
            {
                RiderId = "rider1",
                PickupLatitude = 0.0,
                PickupLongitude = 0.0
            };

            // Mock Redis to simulate no drivers available
            _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<string>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)JsonSerializer.Serialize(new UserLocation { Latitude = 0.0, Longitude = 0.0 }));


            // Act
            var result = await _controller.RequestRide(request);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("No nearby drivers available.", notFoundResult.Value);
        }

        [Fact]
        public async Task GetRideStatus_ShouldReturnOk_WhenRideExists()
        {
            // Arrange
            var rideId = "ride1";
            var rideStatus = new RideStatus
            {
                RideId = rideId,
                RiderId = "rider1",
                DriverId = "driver1",
                Status = "Accepted"
            };

            _mockDatabase.Setup(db => db.HashGetAsync(It.IsAny<string>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(rideStatus));

            // Act
            var result = await _controller.GetRideStatus(rideId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<RideStatus>(okResult.Value);
            Assert.Equal("Accepted", returnValue.Status);
        }
    }
}
