using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace ReincarnationDisplay
{
    // One per HUD. Each frame: is the page usable, which zones are valid, then drive the panel and rings.
    internal sealed class ReincarnationDisplayController : MonoBehaviour
    {
        private readonly List<ZoneInfo> zones = new List<ZoneInfo>();
        private HUD hud;
        private ZoneHudPanel panel;
        private ZoneWorldRings rings;
        private GameObject cachedBodyObject;
        private CharacterBody cachedBody;
        private bool reportedBuffMismatch;

        private void Awake()
        {
            hud = GetComponent<HUD>();
            rings = ZoneWorldRings.Create();
        }

        private void OnDestroy()
        {
            if (rings)
            {
                Destroy(rings.gameObject);
            }
        }

        private void Update()
        {
            PluginConfig settings = ReincarnationDisplayPlugin.Settings;
            CharacterBody body = GetTargetBody();

            zones.Clear();
            int activeIndex = -1;
            bool usable = ZoneScanner.IsPageUsable(body);
            if (usable)
            {
                activeIndex = ZoneScanner.Scan(body, zones);
            }

            if (settings.DebugLogging.Value)
            {
                ZoneDebugDump.LogNewZones();
                ReportBuffMismatch(body, usable, activeIndex >= 0);
            }

            if (!panel && hud.keyItemIcon)
            {
                panel = ZoneHudPanel.Create(hud.keyItemIcon);
            }
            if (panel)
            {
                if (settings.ShowPanel.Value && activeIndex >= 0)
                {
                    panel.Show(zones[activeIndex]);
                }
                else
                {
                    panel.Hide();
                }
            }

            // The rings live in the scene, so they are gone after a stage change if this HUD survived it.
            if (!rings)
            {
                rings = ZoneWorldRings.Create();
            }
            if (usable && settings.ShowWorldRings.Value)
            {
                rings.UpdateRings(zones, body.corePosition, settings.RingMaxDistance.Value);
            }
            else
            {
                rings.Clear();
            }
        }

        private CharacterBody GetTargetBody()
        {
            GameObject bodyObject = hud.targetBodyObject;
            if (bodyObject != cachedBodyObject)
            {
                cachedBodyObject = bodyObject;
                cachedBody = bodyObject ? bodyObject.GetComponent<CharacterBody>() : null;
            }
            return cachedBody;
        }

        // The server's CursedBookReadyForSacrifice buff is the authoritative in-zone signal; log when ours disagrees.
        private void ReportBuffMismatch(CharacterBody body, bool usable, bool inZone)
        {
            bool mismatch = usable && ZoneScanner.HasReadyBuff(body) != inZone;
            if (mismatch && !reportedBuffMismatch)
            {
                ReincarnationDisplayPlugin.Log.LogWarning(
                    $"In-zone mismatch: local check says {inZone}, server buff says {!inZone} at {body.corePosition}");
            }
            reportedBuffMismatch = mismatch;
        }
    }
}
