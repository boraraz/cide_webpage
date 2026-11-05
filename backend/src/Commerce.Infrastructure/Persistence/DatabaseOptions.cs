using System.ComponentModel.DataAnnotations;

namespace Commerce.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "Database";

    [Required(ErrorMessage =
        "ConnectionStrings:Database is not configured. For local development run `npm run api:secrets`.")]
    public string ConnectionString { get; set; } = string.Empty;
}
