using System.Collections.Concurrent;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace KappaTracker.Services
{
    /// <summary>
    /// Fetches item icons once from assets.tarkov.dev and keeps them in memory only.
    /// Nothing is written to the user's mod folder; the cache is rebuilt on each
    /// server start. Concurrent requests for the same icon are collapsed into one
    /// fetch, and misses are remembered so a missing icon is not re-requested.
    /// </summary>
    [Injectable(InjectionType.Singleton)]
    public class IconCacheService
    {
        private const string RemoteFormat = "https://assets.tarkov.dev/{0}-icon.webp";

        private readonly ISptLogger<IconCacheService> _logger;

        private readonly ConcurrentDictionary<string, byte[]?> _memory = new();
        private readonly ConcurrentDictionary<string, Task<byte[]?>> _inFlight = new();
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        public IconCacheService(ISptLogger<IconCacheService> logger)
        {
            _logger = logger;
        }

        public Task<byte[]?> GetIconAsync(string templateId)
        {
            if (_memory.TryGetValue(templateId, out var cached))
                return Task.FromResult(cached);

            // Collapse concurrent requests for the same icon into a single fetch.
            return _inFlight.GetOrAdd(templateId, LoadAsync);
        }

        private async Task<byte[]?> LoadAsync(string templateId)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(string.Format(RemoteFormat, templateId));
                _memory[templateId] = bytes;
                return bytes;
            }
            catch (Exception ex)
            {
                // Remember the miss so we don't hammer the network for a missing icon.
                _memory[templateId] = null;
                _logger.Debug($"[KappaTracker] No icon for {templateId}: {ex.Message}");
                return null;
            }
            finally
            {
                _inFlight.TryRemove(templateId, out _);
            }
        }
    }
}
