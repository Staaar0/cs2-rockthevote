using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Memory;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace cs2_rockthevote
{
    public class GameRules : IPluginDependency<Plugin, Config>
    {
        CCSGameRules? _gameRules = null;
        bool? _networkedGameRestart = null;

        public void SetGameRules()
        {
            _gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            _networkedGameRestart = null;
        }

        public void SetGameRulesAsync()
        {
            _gameRules = null;
            new Timer(1.0F, () =>
            {
                SetGameRules();
            });
        }

        public void OnLoad(Plugin plugin)
        {
            SetGameRulesAsync();
            plugin.RegisterEventHandler<EventRoundStart>(OnRoundStart);
            plugin.RegisterEventHandler<EventRoundAnnounceWarmup>(OnAnnounceWarmup);
        }

        public float GameStartTime => _gameRules?.GameStartTime ?? 0;

        public void OnMapStart(string map)
        {
            SetGameRulesAsync();
        }


        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            SetGameRules();
            return HookResult.Continue;
        }

        public HookResult OnAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            SetGameRules();
            return HookResult.Continue;
        }

        public bool WarmupRunning => _gameRules?.WarmupPeriod ?? false;

        public int TotalRoundsPlayed => _gameRules?.TotalRoundsPlayed ?? 0;

        public void PreventHtmlHudFlashing()
        {
            if (_gameRules is null)
                return;

            bool gameRestart = _gameRules.RestartRoundTime < Server.CurrentTime;
            if (_gameRules.GameRestart == gameRestart && _networkedGameRestart == gameRestart)
                return;

            _gameRules.GameRestart = gameRestart;
            NativeAPI.SchemaNetworkStateChanged(
                _gameRules.Handle + Schema.GetSchemaOffset("CGameRules", "__m_pChainEntity"),
                (uint)Schema.GetSchemaOffset("CCSGameRules", "m_bGameRestart"),
                uint.MaxValue, uint.MaxValue);
            _networkedGameRestart = gameRestart;
        }
    }
}
