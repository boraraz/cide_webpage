using Commerce.Application.Catalog;

namespace Commerce.IntegrationTests.Catalog;

public sealed class MemoryObjectStorage : IObjectStorage
{
    public Dictionary<string, StoredObject> Objects { get; } = new(StringComparer.Ordinal);

    public Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        Objects[key] = new StoredObject(content.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task<StoredObject?> OpenAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Objects.TryGetValue(key, out var stored) ? stored : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Objects.Remove(key);
        return Task.CompletedTask;
    }
}
