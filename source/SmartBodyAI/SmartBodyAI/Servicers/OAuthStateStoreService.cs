using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace SmartBodyAI.Servicers;

public class OAuthStateStoreService
{
    private const string KeyPrefix = "oauth:state:";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;

    public OAuthStateStoreService(IDistributedCache cache) => _cache = cache;

    public async Task<string> SaveAsync<T>(string stateId, T state, TimeSpan ttl, CancellationToken ct = default)
    {
        var key = KeyPrefix + stateId;

        var json = JsonSerializer.Serialize(state, JsonOpts);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        await _cache.SetStringAsync(key, json, options, ct);
        return stateId;
    }

    public async Task<T?> LoadAsync<T>(string stateId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stateId)) return default;

        var key = KeyPrefix + stateId;
        var json = await _cache.GetStringAsync(key, ct);

        if (string.IsNullOrWhiteSpace(json)) return default;

        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }

    public Task RemoveAsync(string stateId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stateId)) return Task.CompletedTask;
        return _cache.RemoveAsync(KeyPrefix + stateId, ct);
    }
}
