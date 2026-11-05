using System.Net;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Json;
using Commerce.Application.Identity;
using Commerce.Domain.Identity;
using Commerce.Infrastructure.Identity;
using Commerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Commerce.IntegrationTests.Identity;

[Collection(IdentityCollection.Name)]
public sealed class IdentityEndpointTests(IdentityApiFactory factory)
{
    [Fact]
    public async Task Migration_creates_the_identity_schema_and_role_seed_is_idempotent()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var migrations = await database.Database.GetAppliedMigrationsAsync();
        Assert.Contains(migrations, name => name.EndsWith("_InitialIdentity", StringComparison.Ordinal));
        var tables = await database.Database.SqlQueryRaw<TableName>(
            """SELECT tablename AS "Value" FROM pg_tables WHERE schemaname = 'public'""").ToListAsync();
        Assert.Contains(tables, table => table.Value == "users");
        Assert.Contains(tables, table => table.Value == "auth_sessions");
        Assert.Contains(tables, table => table.Value == "security_events");

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);
        Assert.Equal(2, await database.Roles.CountAsync());
    }

    [Fact]
    public async Task Register_hashes_the_password_and_rejects_duplicates_and_invalid_data()
    {
        var email = AuthHttp.EmailAddress();
        using var client = factory.CreateClient();
        var created = await AuthHttp.RegisterAsync(client, email);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<UserBody>();
        Assert.Equal(email, body!.Email);
        Assert.False(body.EmailVerified);
        Assert.Contains(RoleNames.Customer, body.Roles);
        Assert.DoesNotContain(RoleNames.Admin, body.Roles);

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var user = await database.Users.SingleAsync(candidate => candidate.Email == email);
            Assert.Equal(email.ToUpperInvariant(), user.NormalizedEmail);
            Assert.NotEqual(AuthHttp.Password, user.PasswordHash);
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            Assert.True(hasher.Verify(user, AuthHttp.Password).Succeeded);
        }

        var duplicate = await AuthHttp.RegisterAsync(client, email.ToUpperInvariant());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        await AuthHttp.PrepareAsync(client);
        var invalid = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "",
            lastName = "Lovelace",
            email = "not-an-email",
            password = "short",
            passwordConfirmation = "other",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Login_accepts_the_customer_and_rejects_bad_inactive_and_admin_attempts()
    {
        var email = AuthHttp.EmailAddress();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(client, email)).StatusCode);

        var wrong = await AuthHttp.LoginAsync(client, email, "WrongPassword1");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var unknown = await AuthHttp.LoginAsync(client, AuthHttp.EmailAddress());
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);

        var signedIn = await AuthHttp.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var adminMe = await client.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.Forbidden, adminMe.StatusCode);

        var adminLogin = await AuthHttp.LoginAsync(client, email, path: "/api/v1/admin/auth/login");
        Assert.Equal(HttpStatusCode.Unauthorized, adminLogin.StatusCode);

        await DeactivateAsync(email);
        var inactive = await AuthHttp.LoginAsync(factory.CreateClient(), email);
        Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
    }

    [Fact]
    public async Task Admin_login_requires_the_admin_role()
    {
        const string email = "admin-login@example.com";
        await EnsureAdminAsync(email);
        using var client = factory.CreateClient();
        var login = await AuthHttp.LoginAsync(client, email, path: "/api/v1/admin/auth/login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await client.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var storefront = await AuthHttp.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Unauthorized, storefront.StatusCode);
    }

    [Fact]
    public async Task Logout_and_logout_all_revoke_sessions()
    {
        var email = AuthHttp.EmailAddress();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(first, email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthHttp.LoginAsync(second, email)).StatusCode);

        await AuthHttp.PrepareAsync(first);
        var logout = await first.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/v1/auth/me")).StatusCode);

        await AuthHttp.PrepareAsync(second);
        var logoutAll = await second.PostAsync("/api/v1/auth/logout-all", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Expired_and_reused_session_tokens_are_rejected()
    {
        var email = AuthHttp.EmailAddress();
        var jar = new CookieJar(factory.Server.CreateHandler());
        using var client = new HttpClient(jar) { BaseAddress = factory.Server.BaseAddress };
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(client, email)).StatusCode);
        await ExpireSessionAsync(email);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        var login = await AuthHttp.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var original = jar.Session;
        Assert.NotNull(original);
        await AgeSessionForRotationAsync(email);
        var rotated = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.NotNull(jar.Session);
        Assert.NotEqual(original, jar.Session);

        using var replay = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", $"cide-session={original}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Profile_updates_the_allowed_fields_and_password_change_revokes_sessions()
    {
        var email = AuthHttp.EmailAddress();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(client, email)).StatusCode);

        var profile = await client.GetAsync("/api/v1/account/profile");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        await AuthHttp.PrepareAsync(client);
        var updated = await client.PatchAsJsonAsync("/api/v1/account/profile", new
        {
            firstName = "Grace",
            lastName = "Hopper",
            phoneNumber = "0532 000 00 00",
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<UserBody>();
        Assert.Equal("Grace", body!.FirstName);
        Assert.Equal("+905320000000", body.PhoneNumber);
        Assert.Equal(email, body.Email);

        await AuthHttp.PrepareAsync(client);
        var badPhone = await client.PatchAsJsonAsync("/api/v1/account/profile", new
        {
            firstName = "Grace",
            lastName = "Hopper",
            phoneNumber = "+1 202 555 0100",
        });
        Assert.Equal(HttpStatusCode.BadRequest, badPhone.StatusCode);

        await AuthHttp.PrepareAsync(client);
        var changed = await client.PostAsJsonAsync("/api/v1/account/change-password", new
        {
            currentPassword = AuthHttp.Password,
            newPassword = "NewPassword123",
            passwordConfirmation = "NewPassword123",
        });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthHttp.LoginAsync(client, email, "NewPassword123")).StatusCode);
    }

    [Fact]
    public async Task Email_verification_is_single_use_and_expires()
    {
        factory.Email.Clear();
        var email = AuthHttp.EmailAddress();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(client, email)).StatusCode);
        var token = factory.Email.LatestToken();

        var confirm = await AuthHttp.SendAsync(client, "/api/v1/auth/email-verification/confirm", new { token });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        var me = await (await client.GetAsync("/api/v1/auth/me")).Content.ReadFromJsonAsync<UserBody>();
        Assert.True(me!.EmailVerified);

        var reuse = await AuthHttp.SendAsync(client, "/api/v1/auth/email-verification/confirm", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        factory.Email.Clear();
        var second = AuthHttp.EmailAddress();
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(factory.CreateClient(), second)).StatusCode);
        await ExpireTokenAsync(second, verification: true);
        var expired = await AuthHttp.SendAsync(client, "/api/v1/auth/email-verification/confirm", new { token = factory.Email.LatestToken() });
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
    }

    [Fact]
    public async Task Password_reset_is_generic_single_use_and_revokes_sessions()
    {
        var email = AuthHttp.EmailAddress();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(client, email)).StatusCode);

        factory.Email.Clear();
        var missing = await AuthHttp.SendAsync(client, "/api/v1/auth/password-reset/request", new { email = AuthHttp.EmailAddress() });
        var present = await AuthHttp.SendAsync(client, "/api/v1/auth/password-reset/request", new { email });
        Assert.Equal(HttpStatusCode.Accepted, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, present.StatusCode);
        Assert.Single(factory.Email.Messages);

        var token = factory.Email.LatestToken();
        var confirm = await AuthHttp.SendAsync(client, "/api/v1/auth/password-reset/confirm", new
        {
            token,
            password = "ResetPassword123",
            passwordConfirmation = "ResetPassword123",
        });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthHttp.LoginAsync(client, email, "ResetPassword123")).StatusCode);

        var reuse = await AuthHttp.SendAsync(client, "/api/v1/auth/password-reset/confirm", new
        {
            token,
            password = "AnotherPassword123",
            passwordConfirmation = "AnotherPassword123",
        });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        factory.Email.Clear();
        await AuthHttp.SendAsync(client, "/api/v1/auth/password-reset/request", new { email });
        await ExpireTokenAsync(email, verification: false);
        var expired = await AuthHttp.SendAsync(client, "/api/v1/auth/password-reset/confirm", new
        {
            token = factory.Email.LatestToken(),
            password = "AnotherPassword123",
            passwordConfirmation = "AnotherPassword123",
        });
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
    }

    [Fact]
    public async Task Normalized_email_is_unique_in_the_database()
    {
        var email = AuthHttp.EmailAddress();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(client, email)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        database.Users.Add(new User
        {
            Id = Guid.CreateVersion7(),
            Email = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "not-a-real-hash",
            FirstName = "Dup",
            LastName = "User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }

    [Fact]
    public async Task Admin_bootstrap_is_idempotent_and_refused_in_production()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var seeder = new IdentitySeeder(database, hasher, Options.Create(new IdentitySeedOptions
            {
                SeedRoles = true,
                BootstrapAdmin = true,
                AdminEmail = "bootstrap@example.com",
                AdminPassword = AuthHttp.Password,
            }), TimeProvider.System);

            await seeder.SeedAsync(CancellationToken.None);
            var hash = await database.Users.Where(user => user.Email == "bootstrap@example.com").Select(user => user.PasswordHash).SingleAsync();
            await seeder.SeedAsync(CancellationToken.None);
            var again = await database.Users.Where(user => user.Email == "bootstrap@example.com").Select(user => user.PasswordHash).SingleAsync();
            Assert.Equal(hash, again);
            Assert.Equal(1, await database.Users.CountAsync(user => user.NormalizedEmail == "BOOTSTRAP@EXAMPLE.COM"));
        }

        using var production = new ProductionBootstrapFactory();
        var exception = Assert.ThrowsAny<Exception>(() => production.CreateClient());
        Assert.Contains("BootstrapAdmin", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task DeactivateAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await database.Users.Where(user => user.Email == email)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsActive, false));
    }

    private async Task EnsureAdminAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        if (await database.Users.AnyAsync(user => user.Email == email))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var role = await database.Roles.SingleAsync(candidate => candidate.Name == RoleNames.Admin);
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Admin",
            LastName = "User",
            IsActive = true,
            EmailVerifiedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = hasher.Hash(user, AuthHttp.Password);
        database.Users.Add(user);
        database.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await database.SaveChangesAsync();
    }

    private async Task ExpireSessionAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var userId = await database.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        await database.AuthSessions.Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    private async Task AgeSessionForRotationAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var userId = await database.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        var now = DateTimeOffset.UtcNow;
        await database.AuthSessions.Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.CreatedAt, now.AddDays(-10))
                .SetProperty(session => session.ExpiresAt, now.AddDays(4)));
    }

    private async Task ExpireTokenAsync(string email, bool verification)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var userId = await database.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        var expired = DateTimeOffset.UtcNow.AddMinutes(-5);
        if (verification)
        {
            await database.EmailVerificationTokens.Where(token => token.UserId == userId && token.ConsumedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ExpiresAt, expired));
        }
        else
        {
            await database.PasswordResetTokens.Where(token => token.UserId == userId && token.ConsumedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ExpiresAt, expired));
        }
    }

    private sealed class CookieJar(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private readonly List<string> cookies = [];

        public string? Session { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (cookies.Count > 0)
            {
                request.Headers.Remove("Cookie");
                request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies));
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                foreach (var value in values)
                {
                    var pair = value.Split(';', 2)[0];
                    var name = pair.Split('=', 2)[0];
                    cookies.RemoveAll(existing => existing.StartsWith(name + "=", StringComparison.Ordinal));
                    cookies.Add(pair);
                    if (name == "cide-session")
                    {
                        Session = pair[(name.Length + 1)..];
                    }
                }
            }

            return response;
        }
    }

    private sealed record TableName(string Value);

    private sealed record UserBody(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        string? PhoneNumber,
        bool EmailVerified,
        IReadOnlyList<string> Roles);
}

public sealed class RateLimitTests(TightLimitApiFactory factory) : IClassFixture<TightLimitApiFactory>
{
    [Fact]
    public async Task Registration_is_rate_limited()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(client, AuthHttp.EmailAddress())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AuthHttp.RegisterAsync(client, AuthHttp.EmailAddress())).StatusCode);
        var limited = await AuthHttp.RegisterAsync(client, AuthHttp.EmailAddress());
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}

file sealed class ProductionBootstrapFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Database", "Host=127.0.0.1;Port=1;Database=none;Username=test;Password=test");
        builder.UseSetting("ObjectStorage:BucketName", "test-bucket");
        builder.UseSetting("ObjectStorage:Region", "us-east-1");
        builder.UseSetting("Identity:BootstrapAdmin", "true");
        builder.UseSetting("Identity:AdminEmail", "admin@example.com");
        builder.UseSetting("Identity:AdminPassword", "Password123");
        builder.UseSetting("AllowedHosts", "localhost");
    }
}
