using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.Support;

namespace Tests
{
    public class ProjectTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public ProjectTests(ApiFactory factory) => _factory = factory;

        private static async Task<JsonElement> CreateAsync(HttpClient admin, object body)
        {
            var response = await admin.PostAsJsonAsync("/api/projects", body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await response.ReadAsync();
        }

        [Fact]
        public async Task AnonymousCallersAreRejected()
        {
            var response = await _factory.CreateClient().GetAsync("/api/projects");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RegularUsersCanReadButNotChangeProjects()
        {
            var admin = await _factory.CreateAdminClientAsync();
            var user = await _factory.CreateUserClientAsync();
            var created = await CreateAsync(admin, Json.NewProject());
            var id = created.GetProperty("id").GetString();

            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/projects")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/projects/{id}")).StatusCode);

            Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/projects", Json.NewProject())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.PutAsJsonAsync($"/api/projects/{id}", Json.NewProject())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.DeleteAsync($"/api/projects/{id}")).StatusCode);
        }

        [Fact]
        public async Task AdminCanCreateReadUpdateAndDelete()
        {
            var admin = await _factory.CreateAdminClientAsync();

            var created = await CreateAsync(admin, Json.NewProject(name: "Gedung A", progress: 25));
            var id = created.GetProperty("id").GetString()!;
            Assert.Matches("^[A-Za-z0-9]{8,}$", id);
            Assert.Equal("On Progress", created.GetProperty("status").GetString());

            var read = await (await admin.GetAsync($"/api/projects/{id}")).ReadAsync();
            Assert.Equal("Gedung A", read.GetProperty("projectName").GetString());

            var update = await admin.PutAsJsonAsync($"/api/projects/{id}", Json.NewProject(name: "Gedung A (revisi)", progress: 60));
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var updated = await update.ReadAsync();
            Assert.Equal("Gedung A (revisi)", updated.GetProperty("projectName").GetString());
            Assert.Equal(60, updated.GetProperty("progress").GetInt32());

            Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/projects/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/projects/{id}")).StatusCode);
        }

        [Fact]
        public async Task UnknownAndMalformedIdsAreNotFound()
        {
            var admin = await _factory.CreateAdminClientAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/projects/zzzzzzzz")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/projects/12345")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/projects/zzzzzzzz", Json.NewProject())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/projects/zzzzzzzz")).StatusCode);
        }

        [Fact]
        public async Task DuplicateNamesAreAConflictOnCreateAndUpdate()
        {
            var admin = await _factory.CreateAdminClientAsync();
            var name = $"Unik {Guid.NewGuid():N}";
            await CreateAsync(admin, Json.NewProject(name: name));
            var other = await CreateAsync(admin, Json.NewProject());

            var create = await admin.PostAsJsonAsync("/api/projects", Json.NewProject(name: name));
            Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
            Assert.Equal("Nama project sudah digunakan.", await create.DetailAsync());

            var rename = await admin.PutAsJsonAsync($"/api/projects/{other.GetProperty("id").GetString()}", Json.NewProject(name: name));
            Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
        }

        [Fact]
        public async Task ValidationRulesAreEnforced()
        {
            var admin = await _factory.CreateAdminClientAsync();

            // end before start
            Assert.Equal(HttpStatusCode.BadRequest,
                (await admin.PostAsJsonAsync("/api/projects", Json.NewProject(startOffsetDays: 10, endOffsetDays: 1))).StatusCode);
            // Completed but not at 100%
            Assert.Equal(HttpStatusCode.BadRequest,
                (await admin.PostAsJsonAsync("/api/projects", Json.NewProject(status: "Completed", progress: 50))).StatusCode);
            // 100% but not Completed
            Assert.Equal(HttpStatusCode.BadRequest,
                (await admin.PostAsJsonAsync("/api/projects", Json.NewProject(status: "On Progress", progress: 100))).StatusCode);
            // out of range / unknown status / missing name
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/projects", Json.NewProject(progress: 101))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/projects", Json.NewProject(status: "Done"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/projects", Json.NewProject(name: ""))).StatusCode);
        }

        [Fact]
        public async Task OverdueIsDerivedFromTheEndDateNotStored()
        {
            var admin = await _factory.CreateAdminClientAsync();

            var late = await CreateAsync(admin, Json.NewProject(progress: 30, startOffsetDays: -60, endOffsetDays: -5));
            Assert.Equal("Overdue", late.GetProperty("status").GetString());

            // Marking a late project Completed (at 100%) clears the overdue flag.
            var id = late.GetProperty("id").GetString();
            var finished = await (await admin.PutAsJsonAsync($"/api/projects/{id}",
                Json.NewProject(name: late.GetProperty("projectName").GetString(), status: "Completed", progress: 100, startOffsetDays: -60, endOffsetDays: -5))).ReadAsync();
            Assert.Equal("Completed", finished.GetProperty("status").GetString());

            // Asking for "Overdue" explicitly on a project that ends in the future does not make it overdue.
            var future = await CreateAsync(admin, Json.NewProject(status: "Overdue", progress: 20, endOffsetDays: 20));
            Assert.Equal("On Progress", future.GetProperty("status").GetString());
        }

        [Fact]
        public async Task ListSupportsPagingFilteringSearchAndSorting()
        {
            var admin = await _factory.CreateAdminClientAsync();
            var tag = Guid.NewGuid().ToString("N")[..8];
            await CreateAsync(admin, Json.NewProject(name: $"{tag}-alpha", progress: 10));
            await CreateAsync(admin, Json.NewProject(name: $"{tag}-bravo", progress: 90));
            await CreateAsync(admin, Json.NewProject(name: $"{tag}-charlie", progress: 50, startOffsetDays: -90, endOffsetDays: -10));
            await CreateAsync(admin, Json.NewProject(name: $"{tag}-delta", status: "Completed", progress: 100));

            // search narrows to this test's rows
            var all = await (await admin.GetAsync($"/api/projects?search={tag}&pageSize=10")).ReadAsync();
            Assert.Equal(4, all.GetProperty("total").GetInt32());
            Assert.Equal(1, all.GetProperty("totalPages").GetInt32());

            // paging: 2 per page, 2 pages, no overlap
            var page1 = await (await admin.GetAsync($"/api/projects?search={tag}&pageSize=2&page=1")).ReadAsync();
            var page2 = await (await admin.GetAsync($"/api/projects?search={tag}&pageSize=2&page=2")).ReadAsync();
            Assert.Equal(2, page1.GetProperty("totalPages").GetInt32());
            var ids = page1.GetProperty("items").EnumerateArray().Concat(page2.GetProperty("items").EnumerateArray())
                .Select(p => p.GetProperty("id").GetString()).ToList();
            Assert.Equal(4, ids.Distinct().Count());

            // status filters use the derived status
            var overdue = await (await admin.GetAsync($"/api/projects?search={tag}&status=Overdue")).ReadAsync();
            Assert.Equal(1, overdue.GetProperty("total").GetInt32());
            Assert.EndsWith("charlie", overdue.GetProperty("items")[0].GetProperty("projectName").GetString());

            var onProgress = await (await admin.GetAsync($"/api/projects?search={tag}&status=On%20Progress")).ReadAsync();
            Assert.Equal(2, onProgress.GetProperty("total").GetInt32());

            var completed = await (await admin.GetAsync($"/api/projects?search={tag}&status=Completed")).ReadAsync();
            Assert.Equal(1, completed.GetProperty("total").GetInt32());

            // sorting
            var byProgress = await (await admin.GetAsync($"/api/projects?search={tag}&sortBy=progress&sortOrder=desc")).ReadAsync();
            var order = byProgress.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("progress").GetInt32()).ToList();
            Assert.Equal(order.OrderByDescending(x => x).ToList(), order);
        }

        [Theory]
        [InlineData("pageSize=101")]
        [InlineData("pageSize=0")]
        [InlineData("page=0")]
        [InlineData("status=Done")]
        [InlineData("sortBy=password")]
        [InlineData("sortOrder=sideways")]
        public async Task BadListQueriesAreRejected(string query)
        {
            var admin = await _factory.CreateAdminClientAsync();
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/projects?{query}")).StatusCode);
        }

        [Fact]
        public async Task SearchTreatsWildcardsAsPlainText()
        {
            var admin = await _factory.CreateAdminClientAsync();
            await CreateAsync(admin, Json.NewProject(name: $"Normal {Guid.NewGuid():N}"));

            var result = await (await admin.GetAsync("/api/projects?search=%25")).ReadAsync();
            Assert.Equal(0, result.GetProperty("total").GetInt32());
        }
    }
}
