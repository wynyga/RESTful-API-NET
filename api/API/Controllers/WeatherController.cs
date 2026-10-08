using System.Text.RegularExpressions;
using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // an open endpoint would let anyone use this API as a free proxy to the weather provider
    [EnableRateLimiting("weather")]
    public partial class WeatherController : ControllerBase
    {
        private readonly IWeatherService _weatherService;

        public WeatherController(IWeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        // Letters (any script), digits, spaces and . ' - only; 1-60 characters.
        [GeneratedRegex(@"^[\p{L}\p{N} .'\-]{1,60}$")]
        private static partial Regex CityPattern();

        [HttpGet("{city}")]
        public async Task<IActionResult> GetWeather(string city, CancellationToken ct)
        {
            if (!CityPattern().IsMatch(city))
            {
                return Problem(detail: "Nama kota tidak valid.", statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var weather = await _weatherService.GetWeatherAsync(city, ct);
                return Ok(weather);
            }
            catch (KeyNotFoundException ex)
            {
                // 404 Not Found (Kota tidak ada)
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
            }
            catch (TimeoutException)
            {
                // 504 Gateway Timeout (Layanan cuaca lambat)
                return Problem(detail: "Layanan cuaca sedang tidak merespons (Timeout). Silakan coba lagi nanti.", statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (InvalidOperationException)
            {
                // 502 Bad Gateway (Layanan cuaca mengembalikan data sampah)
                return Problem(detail: "Menerima data yang tidak valid dari layanan cuaca.", statusCode: StatusCodes.Status502BadGateway);
            }
            catch (HttpRequestException)
            {
                // 502 Bad Gateway (Layanan cuaca error 500 atau mati)
                return Problem(detail: "Gagal terhubung ke layanan cuaca saat ini.", statusCode: StatusCodes.Status502BadGateway);
            }
            // Anything else is a bug: let the global handler log it and answer with a generic 500.
        }
    }
}
