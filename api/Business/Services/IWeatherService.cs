using Models.DTOs;

namespace Business.Services
{
    public interface IWeatherService
    {
        /// <exception cref="KeyNotFoundException">The city is unknown to the weather provider.</exception>
        /// <exception cref="TimeoutException">The provider did not answer in time.</exception>
        /// <exception cref="InvalidOperationException">The provider answered with something unusable.</exception>
        /// <exception cref="HttpRequestException">The provider is unreachable or returned an error status.</exception>
        Task<WeatherResponseDTO> GetWeatherAsync(string city, CancellationToken ct = default);
    }
}
