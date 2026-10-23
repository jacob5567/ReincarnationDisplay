using RoR2.UI;
using TMPro;

namespace ReincarnationDisplay
{
    // RiskUI (bubbet.riskui) replaces the game's HUD prefab with its own. Nothing here references RiskUI's assembly.
    internal static class RiskUICompat
    {
        // The game instantiates the HUD prefab, so a RiskUI HUD object is named "RiskUI(Clone)".
        private const string HudPrefabName = "RiskUI";

        // True only when RiskUI is installed and enabled: it swaps in its prefab when a HUD is created, and its Enabled
        // setting only applies to HUDs created after the change, so the HUD itself is the reliable signal.
        public static bool IsRiskUIHud(HUD hud)
        {
            return hud && hud.name.StartsWith(HudPrefabName);
        }

        // RiskUI's own labels (health, money, cooldowns, boss name) use the font its asset bundle ships, while its
        // HGTextMeshProUGUI labels keep the game font. Copies the former from the money counter. Call after the text's
        // Awake, which is where HGTextMeshProUGUI applies the game font.
        public static void ApplyFont(HUD hud, TMP_Text text)
        {
            TextMeshProUGUI source = hud.moneyText ? hud.moneyText.targetText : null;
            if (!source || !source.font) return;

            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
        }
    }
}
