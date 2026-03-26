using System;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;

namespace CS2Stats.Plugin;

[MinimumApiVersion(80)]
public sealed class CS2StatsPlugin : BasePlugin, IPluginConfig<PluginConfig>
{
    private readonly SemaphoreSlim _flushGate = new(1, 1);

    private StatsCaptureService _capture = new();
    private IStatsWriter _writer = null!;
    private int _flushIntervalSeconds = 15;
    private int _presenceIntervalSeconds = 10;

    public override string ModuleName => "CS2 Stats";
    public override string ModuleVersion => "0.9.0";
    public override string ModuleAuthor => "CS2 Stats Team";
    public override string ModuleDescription => "Capture and persist CS2 server/player stats.";

    public PluginConfig Config { get; set; } = new();

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
        _flushIntervalSeconds = Math.Max(1, config.Sync.FlushIntervalSeconds);
        _presenceIntervalSeconds = Math.Max(1, config.Sync.PresenceSnapshotIntervalSeconds);
    }

    public override void Load(bool hotReload)
    {
        // Initialize database automatically (create DB, tables, procedures)
        _ = InitializeDatabaseAsync();

        _writer = BuildWriter();
        _capture = new StatsCaptureService("de_mirage");

        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        RegisterEventHandler<EventRoundStart>((@event, _) =>
        {
            _capture.OnRoundStart(@event);
            return HookResult.Continue;
        });

        RegisterEventHandler<EventRoundEnd>((@event, _) =>
        {
            _capture.OnRoundEnd(@event);
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerConnectFull>((@event, _) =>
        {
            _capture.OnPlayerConnectFull(@event);
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            _capture.OnPlayerDisconnect(@event);
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDeath>((@event, _) =>
        {
            _capture.OnPlayerDeath(@event);
            return HookResult.Continue;
        });

        RegisterEventHandler<EventWeaponFire>((@event, _) =>
        {
            _capture.OnWeaponFire(@event);
            return HookResult.Continue;
        });

        RegisterEventHandler<EventBombPlanted>((@event, _) =>
        {
            _capture.OnBombPlanted(@event);
            return HookResult.Continue;
        });

        RegisterEventHandler<EventBombDefused>((@event, _) =>
        {
            _capture.OnBombDefused(@event);
            return HookResult.Continue;
        });

        RegisterEventHandler<EventRoundMvp>((@event, _) =>
        {
            _capture.OnRoundMvp(@event);
            return HookResult.Continue;
        });

        AddTimer(_flushIntervalSeconds, () => _ = FlushAsync(), TimerFlags.REPEAT);
        AddTimer(_presenceIntervalSeconds, () => _capture.CapturePresenceSnapshot(), TimerFlags.REPEAT);

        Logger.LogInformation("CS2Stats plugin loaded with flush interval={Flush}s presence interval={Presence}s",
            _flushIntervalSeconds,
            _presenceIntervalSeconds);
    }

    public override void Unload(bool hotReload)
    {
        _ = FlushAsync();
    }

    private void OnMapStart(string mapName)
    {
        _capture.OnMapStart(mapName);
    }

    private async Task FlushAsync()
    {
        if (!await _flushGate.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var batch = _capture.DrainBatch();
            if (batch.IsEmpty)
            {
                return;
            }

            await _writer.WriteBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while flushing stats batch");
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Config.MySql.Host) || string.IsNullOrWhiteSpace(Config.MySql.Database))
            {
                Logger.LogWarning("[CS2Stats] MySQL config incomplete, database auto-init skipped.");
                return;
            }

            var connectionString = new MySqlConnector.MySqlConnectionStringBuilder
            {
                Server = Config.MySql.Host,
                Port = (uint)Config.MySql.Port,
                    UserID = Config.MySql.Username,
                Password = Config.MySql.Password,
                    SslMode = Config.MySql.SslRequired ? MySqlConnector.MySqlSslMode.Required : MySqlConnector.MySqlSslMode.None,
                AllowUserVariables = true
            }.ConnectionString;

            var initService = new DatabaseInitializationService(connectionString, Config.MySql.Database, Logger);
            await initService.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[CS2Stats] Database initialization failed, plugin will try to persist anyway.");
        }
    }

    private static MySqlConnector.MySqlSslMode GetSslMode(string? configValue)
    {
        return (configValue?.ToLower()) switch
        {
            "required" => MySqlConnector.MySqlSslMode.Required,
            "preferred" => MySqlConnector.MySqlSslMode.Preferred,
            _ => MySqlConnector.MySqlSslMode.None
        };
    }

        private IStatsWriter BuildWriter()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Config.MySql.Host) || string.IsNullOrWhiteSpace(Config.MySql.Database))
            {
                Logger.LogWarning("MySQL config is incomplete, fallback to no-op writer.");
                return new NoOpStatsWriter(Logger);
            }

            return new MySqlStatsWriter(Config.MySql, Logger);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize MySQL writer, fallback to no-op writer.");
            return new NoOpStatsWriter(Logger);
        }
    }
}
