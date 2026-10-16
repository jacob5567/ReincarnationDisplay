using BepInEx.Configuration;
using UnityEngine;

namespace ReincarnationDisplay
{
    internal sealed class PluginConfig
    {
        public readonly ConfigEntry<bool> ShowPanel;
        public readonly ConfigEntry<bool> ShowZoneIndicator;
        public readonly ConfigEntry<float> PanelOffsetY;
        public readonly ConfigEntry<float> PanelScale;

        public readonly ConfigEntry<bool> ShowWorldRings;
        public readonly ConfigEntry<float> RingMaxDistance;

        public readonly ConfigEntry<float> StageStartWindowSeconds;
        public readonly ConfigEntry<bool> HideStageStartZoneAfterWindow;

        public readonly ConfigEntry<bool> DebugLogging;
        public readonly ConfigEntry<bool> DebugAlwaysShow;
        public readonly ConfigEntry<KeyboardShortcut> DumpZonesKey;

        public PluginConfig(ConfigFile config)
        {
            ShowPanel = config.Bind("Panel", "ShowPanel", true,
                "Show the panel above the N'tormat icon while standing in a valid sacrifice zone.");
            ShowZoneIndicator = config.Bind("Panel", "ShowZoneIndicator", true,
                "When not standing in a zone, show a one-line hint above the N'tormat icon if the stage has a sacrifice zone. The stage-start reroll zone doesn't count.");
            PanelOffsetY = config.Bind("Panel", "PanelOffsetY", -6f,
                "Vertical gap, in UI pixels, between the panel and the N'tormat page selector. Applies only when the selector is the highest thing above the icon; otherwise the panel sits a fixed 6 pixels above whatever is there.");
            PanelScale = config.Bind("Panel", "PanelScale", 1f,
                "Scale multiplier for the panel.");

            ShowWorldRings = config.Bind("World", "ShowWorldRings", true,
                "Draw rings on the ground marking the edge of each valid sacrifice zone.");
            RingMaxDistance = config.Bind("World", "RingMaxDistance", 300f,
                "Rings further than this many meters away are hidden.");

            StageStartWindowSeconds = config.Bind("Timing", "StageStartWindowSeconds", 30f,
                "Fallback length of the stage-start reroll window, used only if the zone has no timer of its own.");
            HideStageStartZoneAfterWindow = config.Bind("Timing", "HideStageStartZoneAfterWindow", true,
                "Stop treating the stage-start zone as valid once the window has elapsed.");

            DebugLogging = config.Bind("Debug", "DebugLogging", false,
                "Log every sacrifice zone as it appears, and enable the zone dump key.");
            DebugAlwaysShow = config.Bind("Debug", "DebugAlwaysShow", false,
                "Show the panel and zone rings even without a Reincarnation page, for testing.");
            DumpZonesKey = config.Bind("Debug", "DumpZonesKey", new KeyboardShortcut(KeyCode.F8),
                "With DebugLogging on, press to log every sacrifice zone in the current stage.");
        }
    }
}
