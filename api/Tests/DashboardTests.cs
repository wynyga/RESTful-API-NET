using System.Net;
using System.Net.Http.Json;
using Tests.Support;

namespace Tests
{
    // xUnit builds a new instance of this class for every test, so each test gets a fresh app and
    // an empty database. The summary counts every project, so tests must not share one.
    public class DashboardTests : IDisposable
    {
        private readonly ApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        [Fact]
        public async Task SummaryRequiresLogin()
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/dashboard/summary")).StatusCode);
        }

        [Fact]
        public async Task SummaryOfAnEmptyDatabaseIsAllZeros()
        {
            var user = await _factory.CreateUserClientAsync();
            var summary = await (await user.GetAsync("/api/dashboard/summary")).ReadAsync();

            Assert.Equal(0, summary.GetProperty("totalProject").GetInt32());
            Assert.Equal(0, summary.GetProperty("progressPercentage").GetDouble());
        }

        [Fact]
        public async Task SummaryCountsDerivedStatusesAndAveragesProgress()
        {
            var admin = await _factory.CreateAdminClientAsync();
            // 2 on progress (10, 30), 1 overdue (50), 1 completed (100): average 47.5
            await admin.PostAsJsonAsync("/api/projects", Json.NewProject(progress: 10));
            await admin.PostAsJsonAsync("/api/projects", Json.NewProject(progress: 30));
            await admin.PostAsJsonAsync("/api/projects", Json.NewProject(progress: 50, startOffsetDays: -60, endOffsetDays: -3));
            await admin.PostAsJsonAsync("/api/projects", Json.NewProject(status: "Completed", progress: 100));

            var user = await _factory.CreateUserClientAsync();
            var summary = await (await user.GetAsync("/api/dashboard/summary")).ReadAsync();

            Assert.Equal(4, summary.GetProperty("totalProject").GetInt32());
            Assert.Equal(2, summary.GetProperty("onProgress").GetInt32());
            Assert.Equal(1, summary.GetProperty("overdue").GetInt32());
            Assert.Equal(1, summary.GetProperty("completed").GetInt32());
            Assert.Equal(47.5, summary.GetProperty("progressPercentage").GetDouble());
        }
    }
}
