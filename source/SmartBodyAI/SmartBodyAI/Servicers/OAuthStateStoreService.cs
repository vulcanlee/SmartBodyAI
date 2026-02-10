using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace SmartBodyAI.Servicers;

public class OAuthStateStoreService
{
    private const string KeyPrefix = "oauth:state:";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly ILogger<OAuthStateStoreService> logger;

    public OAuthStateStoreService(ILogger<OAuthStateStoreService> logger,
        IDistributedCache cache)
    {
        this.logger = logger;
        this._cache = cache;
    }
    // public OAuthStateStoreService(IDistributedCache cache) => _cache = cache;

    public async Task<string> SaveAsync<T>(string stateId, T state, TimeSpan ttl, CancellationToken ct = default)
    {
        var key = KeyPrefix + stateId;

        var json = JsonSerializer.Serialize(state, JsonOpts);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        logger.LogInformation("Saving OAuth state: {Key}, TTL: {TTL}", key, ttl);
        await _cache.SetStringAsync(key, json, options, ct);
        logger.LogInformation("OAuth state saved successfully: {Key}", key); return stateId;
    }

    public async Task<T?> LoadAsync<T>(string stateId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stateId)) return default;

        var key = KeyPrefix + stateId;

        logger.LogInformation("Loading OAuth state: {Key}", key);
        var json = await _cache.GetStringAsync(key, ct);

        if (string.IsNullOrWhiteSpace(json)) return default;

        logger.LogInformation("OAuth state loaded: {Key}, Found: {Found}", key, json != null);

        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }

    public async Task RemoveAsync(string stateId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stateId)) return ;
        logger.LogInformation("Removing OAuth state: {Key}", KeyPrefix + stateId);
        await _cache.RemoveAsync(KeyPrefix + stateId, ct);
        logger.LogInformation("OAuth state removed: {Key}", KeyPrefix + stateId);
        return;
    }
}
