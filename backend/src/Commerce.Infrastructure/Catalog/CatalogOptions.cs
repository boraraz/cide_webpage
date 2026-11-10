namespace Commerce.Infrastructure.Catalog;

public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    public bool SeedSampleData { get; set; }

    public int MediaUploadPermitLimit { get; set; } = 30;

    public int MediaUploadWindowMinutes { get; set; } = 60;

    public int LowStockThreshold { get; set; } = 5;
}
