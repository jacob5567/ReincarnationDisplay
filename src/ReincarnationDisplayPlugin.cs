using BepInEx;
using BepInEx.Logging;
using RoR2.UI;

namespace ReincarnationDisplay
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(RiskOfOptionsCompat.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    public class ReincarnationDisplayPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "sevenstack.ReincarnationDisplay";
        public const string PluginName = "ReincarnationDisplay";
        public const string PluginVersion = PluginVersionInfo.Version;

        internal static ManualLogSource Log;
        internal static PluginConfig Settings;

        private void Awake()
        {
            Log = Logger;
            Settings = new PluginConfig(Config);

            if (RiskOfOptionsCompat.Enabled)
            {
                RiskOfOptionsCompat.Register(Settings);
            }

            HUD.onHudTargetChangedGlobal += OnHudTargetChanged;
        }

        private void OnDestroy()
        {
            HUD.onHudTargetChangedGlobal -= OnHudTargetChanged;
        }

        private void Update()
        {
            if (Settings.DebugLogging.Value && Settings.DumpZonesKey.Value.IsDown())
            {
                ZoneDebugDump.DumpAll();
            }
        }

        // Fires whenever any HUD changes the body it is showing, so each HUD gets exactly one controller.
        private static void OnHudTargetChanged(HUD hud)
        {
            if (hud && !hud.GetComponent<ReincarnationDisplayController>())
            {
                hud.gameObject.AddComponent<ReincarnationDisplayController>();
            }
        }
    }
}
