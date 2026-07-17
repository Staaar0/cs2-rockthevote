using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace cs2_rockthevote
{
    public partial class Plugin
    {
        [GameEventHandler(HookMode.Post)]
        public HookResult OnRoundEndMapChanger(EventRoundEnd @event, GameEventInfo info)
        {
            _changeMapManager.ChangeNextMap();
            return HookResult.Continue;
        }

        [GameEventHandler(HookMode.Post)]
        public HookResult OnRoundStartMapChanger(EventRoundStart @event, GameEventInfo info)
        {
            if (!_changeMapManager.ChangeNextMap())
                _changeMapManager.ChangeEndMapInWarmup();

            return HookResult.Continue;
        }
    }

    public class ChangeMapManager : IPluginDependency<Plugin, Config>
    {
        private Plugin? _plugin;
        private StringLocalizer _localizer;
        private PluginState _pluginState;
        private MapLister _mapLister;
        private GameRules _gameRules;

        public string? NextMap { get; private set; } = null;
        private string _prefix = DEFAULT_PREFIX;
        private const string DEFAULT_PREFIX = "rtv.prefix";
        private bool _mapEnd = false;
        private Timer? _changeTimer;

        private Map[] _maps = new Map[0];
        private Config _config = new();

        public ChangeMapManager(StringLocalizer localizer, PluginState pluginState, MapLister mapLister, GameRules gameRules)
        {
            _localizer = localizer;
            _pluginState = pluginState;
            _mapLister = mapLister;
            _gameRules = gameRules;
            _mapLister.EventMapsLoaded += OnMapsLoaded;
        }

        public void OnMapsLoaded(object? sender, Map[] maps)
        {
            _maps = maps;
        }

        public void ScheduleMapChange(string map, bool mapEnd = false, string prefix = DEFAULT_PREFIX)
        {
            NextMap = map;
            _prefix = prefix;
            _pluginState.MapChangeScheduled = true;
            _mapEnd = mapEnd;
            SetNextLevel(map);
        }

        public void OnMapStart(string map)
        {
            _changeTimer?.Kill();
            _changeTimer = null;

            if (string.IsNullOrWhiteSpace(NextMap) || string.Equals(map, NextMap, StringComparison.OrdinalIgnoreCase))
            {
                ResetMapChange();
                return;
            }

            var nextMap = NextMap;
            var mapEnd = _mapEnd;
            _changeTimer = _plugin!.AddTimer(1.0F, () =>
            {
                _changeTimer = null;

                if (!string.Equals(NextMap, nextMap, StringComparison.OrdinalIgnoreCase))
                    return;

                if (string.Equals(Server.MapName, nextMap, StringComparison.OrdinalIgnoreCase))
                {
                    ResetMapChange();
                    return;
                }

                _pluginState.MapChangeScheduled = true;
                ChangeNextMap(mapEnd, 0.0F);
            });
        }

        public bool ChangeNextMap(bool mapEnd = false, float delay = 3.0F)
        {
            if (mapEnd != _mapEnd)
                return false;

            if (!_pluginState.MapChangeScheduled || _changeTimer is not null)
                return false;

            var nextMap = NextMap;
            if (string.IsNullOrWhiteSpace(nextMap))
            {
                _pluginState.MapChangeScheduled = false;
                return false;
            }

            var prefix = _prefix;
            Server.PrintToChatAll(_localizer.LocalizeWithPrefixInternal(prefix, "general.changing-map", nextMap));
            _changeTimer = _plugin!.AddTimer(Math.Max(0.0F, delay), () =>
            {
                _changeTimer = null;

                if (!string.Equals(NextMap, nextMap, StringComparison.OrdinalIgnoreCase))
                    return;

                _pluginState.MapChangeScheduled = false;
                ExecuteMapChange(nextMap);
            });
            return true;
        }

        public bool ChangeEndMapInWarmup()
        {
            if (!_mapEnd || !_gameRules.WarmupRunning || string.IsNullOrWhiteSpace(NextMap))
                return false;

            _changeTimer?.Kill();
            _changeTimer = null;
            _pluginState.MapChangeScheduled = true;
            return ChangeNextMap(true, 0.0F);
        }

        private void SetNextLevel(string mapName)
        {
            Map? map = _maps.FirstOrDefault(x => string.Equals(x.Name, mapName, StringComparison.OrdinalIgnoreCase));
            if (map is not null && Server.IsMapValid(map.Name))
                Server.ExecuteCommand($"nextlevel {map.Name}");
        }

        private void ExecuteMapChange(string mapName)
        {
            Map? map = _maps.FirstOrDefault(x => string.Equals(x.Name, mapName, StringComparison.OrdinalIgnoreCase));
            if (map is not null && Server.IsMapValid(map.Name))
            {
                Server.ExecuteCommand($"changelevel {map.Name}");
            }
            else if (map?.Id is not null)
            {
                Server.ExecuteCommand($"host_workshop_map {map.Id}");
            }
            else
            {
                Server.ExecuteCommand($"ds_workshop_changelevel {mapName}");
            }
        }

        private void ResetMapChange()
        {
            _changeTimer?.Kill();
            _changeTimer = null;
            NextMap = null;
            _prefix = DEFAULT_PREFIX;
            _mapEnd = false;
            _pluginState.MapChangeScheduled = false;
        }

        public void OnConfigParsed(Config config)
        {
            _config = config;
        }

        public void OnLoad(Plugin plugin)
        {
            _plugin = plugin;
            plugin.RegisterEventHandler<EventCsWinPanelMatch>((ev, info) =>
            {
                if (_mapEnd && _pluginState.MapChangeScheduled)
                    ChangeNextMap(true, Math.Max(0.0F, _config.EndOfMapVote.DelayToChangeInTheEnd));

                return HookResult.Continue;
            });

            plugin.RegisterEventHandler<EventRoundAnnounceWarmup>((ev, info) =>
            {
                ChangeEndMapInWarmup();
                return HookResult.Continue;
            });
        }
    }
}
