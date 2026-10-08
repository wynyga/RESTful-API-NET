using System.Net;
using Business.Services;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;

namespace Tests
{
    public class WeatherTests : IClassFixture<WeatherTests.WeatherFactory>
    {
        /// <summary>Stands in for wttr.in: each test sets what the "provider" should do.</summary>
        public sealed class FakeProvider : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
            public List<Uri> Requests { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri!);
                return Task.FromResult(Respond(request));
            }
        }

        public sealed class WeatherFactory : ApiFactory
        {
            public FakeProvider Provider { get; } = new();

            protected override void ConfigureTestServices(IServiceCollection services)
            {
                services.AddHttpClient<IWeatherService, WeatherService>().ConfigurePrimaryHttpMessageHandler(() => Provider);
            }
        }

        private const string GoodPayload =
            """{"current_condition":[{"temp_C":"29","humidity":"74","weatherDesc":[{"value":"Partly cloudy"}]}]}""";

        private readonly WeatherFactory _factory;

        public WeatherTests(WeatherFactory factory)
        {
            _factory = factory;
            _factory.Provider.Requests.Clear();
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

        private async Task<HttpResponseMessage> GetAsync(string city)
        {
            var client = await _factory.CreateUserClientAsync();
            return await client.GetAsync($"/api/weather/{city}");
        }

        [Fact]
        public async Task RequiresLogin()
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/weather/Manado")).StatusCode);
        }

        [Fact]
        public async Task ReturnsTheProvidersConditions()
        {
            _factory.Provider.Respond = _ => Json(GoodPayload);

            var response = await GetAsync($"Manado{Guid.NewGuid():N}"[..14]);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.ReadAsync();
            Assert.Equal(29, json.GetProperty("temperature").GetInt32());
            Assert.Equal(74, json.GetProperty("humidity").GetInt32());
            Assert.Equal("Partly cloudy", json.GetProperty("description").GetString());
        }

        [Fact]
        public async Task RepeatLookupsAreServedFromCache()
        {
            _factory.Provider.Respond = _ => Json(GoodPayload);
            var city = $"Cache{Guid.NewGuid():N}"[..12];

            await GetAsync(city);
            await GetAsync(city.ToUpperInvariant());

            Assert.Single(_factory.Provider.Requests);
        }

        [Fact]
        public async Task UnknownCityIs404()
        {
            _factory.Provider.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

            var response = await GetAsync($"Nowhere{Guid.NewGuid():N}"[..12]);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ProviderErrorIs502()
        {
            _factory.Provider.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

            Assert.Equal(HttpStatusCode.BadGateway, (await GetAsync($"Down{Guid.NewGuid():N}"[..12])).StatusCode);
        }

        [Theory]
        [InlineData("this is not json")]
        [InlineData("""{"unexpected":"shape"}""")]
        [InlineData("""{"current_condition":[]}""")]
        [InlineData("""{"current_condition":[{"temp_C":"hot","humidity":"1","weatherDesc":[{"value":"x"}]}]}""")]
        public async Task UnusablePayloadsAre502NotA404(string payload)
        {
            // A missing JSON property used to surface as KeyNotFoundException, which this
            // endpoint reports as "city not found". It must be a provider problem instead.
            _factory.Provider.Respond = _ => Json(payload);

            var response = await GetAsync($"Weird{Guid.NewGuid():N}"[..12]);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }

        [Fact]
        public async Task ProviderTimeoutIs504()
        {
            _factory.Provider.Respond = _ => throw new TaskCanceledException("simulated HttpClient timeout");

            Assert.Equal(HttpStatusCode.GatewayTimeout, (await GetAsync($"Slow{Guid.NewGuid():N}"[..12])).StatusCode);
        }

        [Theory]
        [InlineData("a%2Fb")]
        [InlineData("x%3Fy")]
        [InlineData("%3Cscript%3E")]
        public async Task SuspiciousCityNamesNeverReachTheProvider(string city)
        {
            var response = await GetAsync(city);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_factory.Provider.Requests);
        }

        [Fact]
        public async Task CityNamesAreEscapedInTheOutgoingRequest()
        {
            _factory.Provider.Respond = _ => Json(GoodPayload);

            await GetAsync("New%20York");

            Assert.Contains("New%20York", _factory.Provider.Requests.Single().AbsoluteUri);
        }
    }
}
