using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using CS2Stats.Contracts;
using Microsoft.Extensions.Logging;

namespace CS2Stats.Plugin;

public sealed class MilestoneWebhookService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly WebhookSettings _settings;
    private readonly string? _serverName;
    private readonly ILogger _logger;

    private readonly Dictionary<ulong, int> _killsPerPlayer = new();
    private readonly Dictionary<ulong, string> _nameCache = new();

    public MilestoneWebhookService(WebhookSettings settings, string? serverName, ILogger logger)
    {
        _settings = settings;
        _serverName = serverName;
        _logger = logger;
    }

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(_settings.Url) && _settings.KillsMilestone > 0;

    public async Task ProcessBatchAsync(StatsBatch batch, CancellationToken cancellationToken)
    {
        if (!IsEnabled) return;

        foreach (var session in batch.SessionOpened)
        {
            if (session.DisplayName != null)
                _nameCache[session.SteamId64] = session.DisplayName;
        }

        var toNotify = new List<(ulong SteamId64, int Value)>();

        foreach (var death in batch.PlayerDeaths)
        {
            if (!death.AttackerSteamId64.HasValue) continue;
            var id = death.AttackerSteamId64.Value;

            _killsPerPlayer.TryGetValue(id, out var before);
            var after = before + 1;
            _killsPerPlayer[id] = after;

            var milestone = _settings.KillsMilestone;
            if (before / milestone != after / milestone)
                toNotify.Add((id, after));
        }

        foreach (var (steamId64, value) in toNotify)
        {
            _nameCache.TryGetValue(steamId64, out var name);
            await FireAsync(steamId64, name, value, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task FireAsync(ulong steamId64, string? playerName, int killCount, CancellationToken cancellationToken)
    {
        var payload = new
        {
            @event = "milestone",
            server = _serverName ?? "default",
            steamId64 = steamId64.ToString(),
            playerName,
            milestone = "kills",
            value = killCount,
            timestamp = DateTime.UtcNow
        };

        try
        {
            using var response = await Http.PostAsJsonAsync(_settings.Url, payload, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("[CS2Stats] Webhook returned {StatusCode} for milestone kills={Kills} player={Player}",
                    (int)response.StatusCode, killCount, steamId64);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CS2Stats] Webhook failed for milestone kills={Kills} player={Player}", killCount, steamId64);
        }
    }
}
