using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace ReincarnationDisplay
{
    // Every RiskOfOptions type is touched only inside NoInlining methods, so the mod loads fine without it.
    internal static class RiskOfOptionsCompat
    {
        public const string GUID = "com.rune580.riskofoptions";

        public static bool Enabled => Chainloader.PluginInfos.ContainsKey(GUID);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Register(PluginConfig settings)
        {
            ModSettingsManager.SetModDescription(
                "Shows the Reincarnation page's sacrifice zones, where a sacrifice will send you, and the stage-start window.");

            ModSettingsManager.AddOption(new CheckBoxOption(settings.ShowPanel));
            ModSettingsManager.AddOption(new StepSliderOption(settings.PanelOffsetY,
                new StepSliderConfig { min = -50f, max = 200f, increment = 1f, FormatString = "{0:0}px" }));
            ModSettingsManager.AddOption(new StepSliderOption(settings.PanelScale,
                new StepSliderConfig { min = 0.5f, max = 2f, increment = 0.05f, FormatString = "{0:0.00}x" }));

            ModSettingsManager.AddOption(new CheckBoxOption(settings.ShowWorldRings));
            ModSettingsManager.AddOption(new StepSliderOption(settings.RingMaxDistance,
                new StepSliderConfig { min = 25f, max = 1000f, increment = 25f, FormatString = "{0:0}m" }));

            ModSettingsManager.AddOption(new StepSliderOption(settings.StageStartWindowSeconds,
                new StepSliderConfig { min = 5f, max = 120f, increment = 1f, FormatString = "{0:0}s" }));
            ModSettingsManager.AddOption(new CheckBoxOption(settings.HideStageStartZoneAfterWindow));

            ModSettingsManager.AddOption(new CheckBoxOption(settings.DebugLogging));
            ModSettingsManager.AddOption(new CheckBoxOption(settings.DebugAlwaysShow));
        }
    }
}
