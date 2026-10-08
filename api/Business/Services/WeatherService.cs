using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Models.DTOs;

namespace Business.Services
{
    public class WeatherService : IWeatherService
    {
        private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        public WeatherService(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
        }

        public async Task<WeatherResponseDTO> GetWeatherAsync(string city, CancellationToken ct = default)
        {
            city = city.Trim();
            var key = $"weather:{city.ToLowerInvariant()}";
            if (_cache.TryGetValue(key, out WeatherResponseDTO? cached) && cached is not null)
            {
                return cached;
            }

            var result = await FetchAsync(city, ct);
            _cache.Set(key, result, CacheFor);
            return result;
        }

        private async Task<WeatherResponseDTO> FetchAsync(string city, CancellationToken ct)
        {
            try
            {
                // wttr.in answers with JSON when ?format=j1 is added. The city is escaped so it
                // can never alter the path or query of the outgoing request.
                using var response = await _httpClient.GetAsync($"{Uri.EscapeDataString(city)}?format=j1", ct);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new KeyNotFoundException($"Kota '{city}' tidak ditemukan.");
                }
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(ct);
                return Parse(city, content);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                // HttpClient raises this when its own timeout elapses, as opposed to the caller cancelling.
                throw new TimeoutException("Koneksi ke API Cuaca terlalu lama (Timeout).", ex);
            }
        }

        private static WeatherResponseDTO Parse(string city, string content)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var current = doc.RootElement.GetProperty("current_condition")[0];

                return new WeatherResponseDTO
                {
                    City = city,
                    Temperature = int.Parse(current.GetProperty("temp_C").GetString() ?? "0"),
                    Description = current.GetProperty("weatherDesc")[0].GetProperty("value").GetString() ?? "Unknown",
                    Humidity = int.Parse(current.GetProperty("humidity").GetString() ?? "0"),
                };
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
                                          or FormatException or ArgumentOutOfRangeException or IndexOutOfRangeException)
            {
                // A missing property is KeyNotFoundException, which the controller would otherwise
                // report as "city not found". Any shape we do not expect is an invalid provider response.
                throw new InvalidOperationException("Response dari API Cuaca tidak valid.", ex);
            }
        }
    }
}
