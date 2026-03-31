using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace smart_hr_attendance_payroll_management.Services
{
    public class DistributedCacheService
    {
        private readonly IDistributedCache _cache;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public DistributedCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var json = await _cache.GetStringAsync(key);
            return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            });
        }

        public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
        {
            var cached = await GetAsync<T>(key);
            if (cached is not null && !EqualityComparer<T>.Default.Equals(cached, default!))
                return cached;

            var created = await factory();
            await SetAsync(key, created, ttl);
            return created;
        }

        public Task RemoveAsync(string key) => _cache.RemoveAsync(key);
    }
}
