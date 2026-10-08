using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Tests.Support;

namespace Tests
{
    public class AuthTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public AuthTests(ApiFactory factory) => _factory = factory;

        [Fact]
        public async Task RegisterThenLoginThenReadProfile()
        {
            var email = ApiFactory.NewEmail();
            var client = _factory.CreateClient();

            var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = ApiFactory.Password });
            Assert.Equal(HttpStatusCode.Created, register.StatusCode);

            var signedIn = await ApiFactory.SignInAsync(_factory.CreateClient(), email);
            var profile = await signedIn.GetAsync("/api/profile");
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

            var json = await profile.ReadAsync();
            Assert.Equal(email, json.GetProperty("email").GetString());
            Assert.Equal("User", json.GetProperty("role").GetString());
            // The profile exposes the obfuscated id, never the raw database key.
            Assert.Matches(new Regex("^[A-Za-z0-9]{8,}$"), json.GetProperty("id").GetString()!);
        }

        [Fact]
        public async Task RegisteringTheSameEmailTwiceIsAConflict()
        {
            var email = ApiFactory.NewEmail();
            var client = _factory.CreateClient();
            await client.PostAsJsonAsync("/api/auth/register", new { email, password = ApiFactory.Password });

            var again = await client.PostAsJsonAsync("/api/auth/register", new { email = email.ToUpperInvariant(), password = ApiFactory.Password });

            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal("Email sudah digunakan.", await again.DetailAsync());
        }

        [Fact]
        public async Task LoginIsCaseInsensitiveOnTheEmail()
        {
            var email = ApiFactory.NewEmail();
            var client = _factory.CreateClient();
            await client.PostAsJsonAsync("/api/auth/register", new { email, password = ApiFactory.Password });

            var login = await client.PostAsJsonAsync("/api/auth/login", new { email = $"  {email.ToUpperInvariant()} ", password = ApiFactory.Password });

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        [Fact]
        public async Task WrongPasswordAndUnknownUserGetTheSameAnswer()
        {
            var email = ApiFactory.NewEmail();
            var client = _factory.CreateClient();
            await client.PostAsJsonAsync("/api/auth/register", new { email, password = ApiFactory.Password });

            var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
            var unknownUser = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.NewEmail(), password = "whatever1" });

            Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
            Assert.Equal(await wrongPassword.DetailAsync(), await unknownUser.DetailAsync());
        }

        [Theory]
        [InlineData("not-an-email", "Passw0rd!")]
        [InlineData("someone@test.local", "short")]
        public async Task InvalidRegistrationIsRejectedWithValidationErrors(string email, string password)
        {
            var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await response.ReadAsync()).TryGetProperty("errors", out _));
        }

        [Fact]
        public async Task ProtectedEndpointsRejectMissingAndGarbageTokens()
        {
            var anonymous = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/profile")).StatusCode);

            anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/profile")).StatusCode);
        }

        [Fact]
        public async Task OnlyAdminsCanListUsersAndChangeRoles()
        {
            var user = await _factory.CreateUserClientAsync();
            var admin = await _factory.CreateAdminClientAsync();

            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/admin/users")).StatusCode);

            var list = await admin.GetAsync("/api/admin/users");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var users = await list.ReadAsync();
            Assert.True(users.GetArrayLength() >= 2);
            // Ids in the listing are obfuscated too.
            Assert.Matches(new Regex("^[A-Za-z0-9]{8,}$"), users[0].GetProperty("id").GetString()!);
        }

        [Fact]
        public async Task AdminCanPromoteAUserAndTheRoleIsValidated()
        {
            var admin = await _factory.CreateAdminClientAsync();
            var email = ApiFactory.NewEmail();
            await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password = ApiFactory.Password });

            var users = await (await admin.GetAsync("/api/admin/users")).ReadAsync();
            var target = users.EnumerateArray().First(u => u.GetProperty("email").GetString() == email).GetProperty("id").GetString();

            var invalid = await admin.PutAsJsonAsync($"/api/users/{target}/role", new { role = "Superuser" });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

            var ok = await admin.PutAsJsonAsync($"/api/users/{target}/role", new { role = "Admin" });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

            var missing = await admin.PutAsJsonAsync("/api/users/zzzzzzzz/role", new { role = "Admin" });
            Assert.True(missing.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);
        }
    }
}
