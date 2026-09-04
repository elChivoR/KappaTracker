using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace KappaTracker.Services;

/// <summary>
/// Checks GitHub Releases for a newer version of the mod and exposes the result
/// to the web UI. Fully fail-safe: any network/parse error just means "no update
/// info" and is logged at debug level — it never throws into the UI.
///
/// Only reports something once real Releases are published on the repo
/// (tags alone are not enough for the /releases/latest endpoint).
/// </summary>
[Injectable(InjectionType.Singleton)]
public class UpdateCheckService(ISptLogger<UpdateCheckService> logger)
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/elChivoR/KappaTracker/releases/latest";

    // Be polite to GitHub's unauthenticated rate limit (60 req/h per IP).
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly HttpClient _httpClient = CreateClient();
    private readonly SemaphoreSlim _lock = new(1, 1);

    private UpdateInfo? _cached;
    private DateTime _lastCheckUtc = DateTime.MinValue;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KappaTracker");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>
    /// Returns cached update info, refreshing from GitHub at most once per <see cref="CacheTtl"/>.
    /// Never throws; returns null when the check has not produced a usable result.
    /// </summary>
    public async Task<UpdateInfo?> GetUpdateInfoAsync()
    {
        if (_cached is not null && DateTime.UtcNow - _lastCheckUtc < CacheTtl)
            return _cached;

        await _lock.WaitAsync();
        try
        {
            if (_cached is not null && DateTime.UtcNow - _lastCheckUtc < CacheTtl)
                return _cached;

            _cached = await FetchAsync();
            _lastCheckUtc = DateTime.UtcNow;
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<UpdateInfo?> FetchAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync(ReleasesUrl);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            if (release?.TagName is null || string.IsNullOrWhiteSpace(release.HtmlUrl))
                return null;

            var latestRaw = release.TagName.TrimStart('v', 'V');
            var current = new ModMetadata().Version;
            var latest = new SemanticVersioning.Version(latestRaw, loose: true);

            var available = latest > current;
            if (available)
                logger.Info($"[KappaTracker] Update available: {current} -> {latest} ({release.HtmlUrl})");

            return new UpdateInfo(available, latest.ToString(), release.HtmlUrl);
        }
        catch (Exception ex)
        {
            logger.Debug($"[KappaTracker] Update check failed: {ex.Message}");
            return null;
        }
    }

    private sealed record GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }
    }
}

/// <summary>Result of an update check, surfaced to the web UI.</summary>
public record UpdateInfo(bool Available, string LatestVersion, string Url);
