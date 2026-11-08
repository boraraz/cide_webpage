using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Commerce.IntegrationTests.Catalog;

public sealed class CatalogUpgradeTests
{
    [Fact]
    public async Task Phase2_schema_upgrades_without_losing_identity_rows()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        var userId = Guid.CreateVersion7();

        await using (var database = new CommerceDbContext(options))
        {
            await database.Database.MigrateAsync("20261105152434_InitialIdentity");
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO users (
                    id, email, normalized_email, password_hash, first_name, last_name, is_active, created_at, updated_at)
                VALUES (
                    {userId}, {"phase2@example.com"}, {"phase2@example.com"}, {"hash"}, {"Phase"}, {"Two"}, {true}, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow})
                """);
        }

        await using (var database = new CommerceDbContext(options))
        {
            await database.Database.MigrateAsync();
            var email = await database.Database.SqlQuery<string>(
                $"SELECT email AS \"Value\" FROM users WHERE id = {userId}").SingleAsync();
            Assert.Equal("phase2@example.com", email);
            var tables = await database.Database.SqlQuery<string>(
                $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'products'""")
                .ToListAsync();
            Assert.Contains("products", tables);
        }
    }
}
