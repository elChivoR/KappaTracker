using System.Collections.Concurrent;
using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace KappaTracker.Services
{
    /// <summary>
    /// Fetches item icons once from assets.tarkov.dev and keeps them in memory + on disk
    /// (Mods/KappaTracker/icon-cache) so the same icon is never downloaded twice, even
    /// across server restarts.
    /// </summary>
    [Injectable(InjectionType.Singleton)]
    public class IconCacheService
    {
        private const string RemoteFormat = "https://assets.tarkov.dev/{0}-icon.webp";

        private readonly ISptLogger<IconCacheService> _logger;
        private readonly string _cacheDir;

        private readonly ConcurrentDictionary<string, byte[]?> _memory = new();
        private readonly ConcurrentDictionary<string, Task<byte[]?>> _inFlight = new();
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        public IconCacheService(ISptLogger<IconCacheService> logger, ModHelper modHelper)
        {
            _logger = logger;
            _cacheDir = Path.Combine(
                modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()),
                "icon-cache");
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
                var diskPath = Path.Combine(_cacheDir, templateId + ".webp");
                if (File.Exists(diskPath))
                {
                    var fromDisk = await File.ReadAllBytesAsync(diskPath);
                    _memory[templateId] = fromDisk;
                    return fromDisk;
                }

                var bytes = await _http.GetByteArrayAsync(string.Format(RemoteFormat, templateId));
                _memory[templateId] = bytes;

                try
                {
                    Directory.CreateDirectory(_cacheDir);
                    await File.WriteAllBytesAsync(diskPath, bytes);
                }
                catch (Exception ex)
                {
                    _logger.Warning($"[KappaTracker] Could not write icon cache for {templateId}: {ex.Message}");
                }

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
