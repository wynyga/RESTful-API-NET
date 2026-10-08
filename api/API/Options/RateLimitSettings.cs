namespace API.Options
{
    /// <summary>Requests allowed per client address per minute, bound from "RateLimiting".</summary>
    public sealed class RateLimitSettings
    {
        public const string SectionName = "RateLimiting";

        /// <summary>Login and register, to slow down password guessing.</summary>
        public int AuthPerMinute { get; set; } = 10;

        /// <summary>The weather lookup, which fans out to a third-party service.</summary>
        public int WeatherPerMinute { get; set; } = 30;
    }
}
