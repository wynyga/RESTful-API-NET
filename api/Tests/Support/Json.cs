using System.Net.Http.Json;
using System.Text.Json;

namespace Tests.Support
{
    /// <summary>Small helpers so tests can read just the JSON fields they care about.</summary>
    public static class Json
    {
        public static async Task<JsonElement> ReadAsync(this HttpResponseMessage response)
        {
            var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
            return doc;
        }

        /// <summary>The RFC 9457 "detail" of an error response.</summary>
        public static async Task<string?> DetailAsync(this HttpResponseMessage response)
        {
            var json = await response.ReadAsync();
            return json.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
        }

        public static object NewProject(
            string? name = null,
            string status = "On Progress",
            int progress = 40,
            int startOffsetDays = -10,
            int endOffsetDays = 30,
            string description = "Deskripsi uji") => new
        {
            projectName = name ?? $"Proyek {Guid.NewGuid():N}",
            description,
            status,
            startDate = DateTime.UtcNow.Date.AddDays(startOffsetDays),
            endDate = DateTime.UtcNow.Date.AddDays(endOffsetDays),
            progress,
        };
    }
}
