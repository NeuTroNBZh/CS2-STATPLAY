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
    private AggregationService? _aggregationService;
    private int _flushIntervalSeconds = 15;
    private int _presenceIntervalSeconds = 10;
    private bool _writerDisabledDueToDbAuth;

    public override string ModuleName => "CS2 Stats";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "NeuTroNBZh";
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
        Logger.LogInformation(
            "[CS2Stats] Plugin startup with MySQL target {Host}:{Port}/{Database} (SSL required: {SslRequired})",
            Config.MySql.Host,
            Config.MySql.Port,
            Config.MySql.Database,
            Config.MySql.SslRequired);

        if (MySqlConfigGuard.IsPackagedPlaceholder(Config.MySql))
        {
            _writerDisabledDueToDbAuth = true;
            _writer = new NoOpStatsWriter(Logger);

            Logger.LogError(
                "[CS2Stats] Packaged placeholder MySQL config is still active. Edit addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json with real credentials, then restart the plugin/server. " +
                "Current target={Host}:{Port}/{Database} user={User}",
                Config.MySql.Host,
                Config.MySql.Port,
                Config.MySql.Database,
                Config.MySql.Username);
        }
        else
        {
            // Initialize database automatically (create DB, tables, procedures)
            _ = InitializeDatabaseAsync();
            _writer = BuildWriter();
            _aggregationService = new AggregationService(BuildConnectionStringFromConfig(), Logger);
        }

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
        if (_writerDisabledDueToDbAuth)
        {
            return;
        }

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
            if (_aggregationService != null)
                _ = _aggregationService.RefreshAllStatsAsync(CancellationToken.None);
        }
        catch (MySqlException ex) when (ex.Number == 1045)
        {
            _writerDisabledDueToDbAuth = true;
            _writer = new NoOpStatsWriter(Logger);

            Logger.LogError(ex,
                "[CS2Stats] MySQL authentication failed during flush. Disabling MySQL writer for this runtime to avoid repeated errors. " +
                "Update config credentials and restart plugin/server. Target={Host}:{Port}/{Database} user={User}",
                Config.MySql.Host,
                Config.MySql.Port,
                Config.MySql.Database,
                Config.MySql.Username);
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
            if (MySqlConfigGuard.IsPackagedPlaceholder(Config.MySql))
            {
                Logger.LogWarning("[CS2Stats] Database auto-init skipped because the packaged placeholder MySQL config is still active.");
                return;
            }

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
        catch (MySqlException ex) when (ex.Number == 1045)
        {
            _writerDisabledDueToDbAuth = true;
            _writer = new NoOpStatsWriter(Logger);

            Logger.LogError(ex,
                "[CS2Stats] MySQL authentication failed during database initialization. " +
                "Set valid credentials in plugin config then restart. Target={Host}:{Port}/{Database} user={User}",
                Config.MySql.Host,
                Config.MySql.Port,
                Config.MySql.Database,
                Config.MySql.Username);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[CS2Stats] Database initialization failed, plugin will try to persist anyway.");
        }
    }

    private IStatsWriter BuildWriter()
    {
        try
        {
            if (MySqlConfigGuard.IsPackagedPlaceholder(Config.MySql))
            {
                Logger.LogWarning("[CS2Stats] Using no-op writer because the packaged placeholder MySQL config is still active.");
                return new NoOpStatsWriter(Logger);
            }

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

    private string BuildConnectionStringFromConfig()
    {
        return new MySqlConnector.MySqlConnectionStringBuilder
        {
            Server = Config.MySql.Host,
            Port = (uint)Config.MySql.Port,
            UserID = Config.MySql.Username,
            Password = Config.MySql.Password,
            Database = Config.MySql.Database,
            SslMode = Config.MySql.SslRequired
                ? MySqlConnector.MySqlSslMode.Required
                : MySqlConnector.MySqlSslMode.None,
            AllowUserVariables = true,
            ConnectionTimeout = 15,
            DefaultCommandTimeout = 30
        }.ConnectionString;
    }
}
