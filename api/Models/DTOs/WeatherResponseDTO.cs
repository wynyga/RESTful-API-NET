namespace Models.DTOs
{
    public class WeatherResponseDTO
    {
        public string City { get; set; } = string.Empty;
        public int Temperature { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Humidity { get; set; }
    }
}
