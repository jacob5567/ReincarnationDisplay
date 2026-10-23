using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace ReincarnationDisplay
{
    // One per HUD. Each frame: is the page held (usable or disabled), which zones are valid, then drive the panel and
    // rings.
    internal sealed class ReincarnationDisplayController : MonoBehaviour
    {
        // The buff reaches clients a little after the body crosses a zone edge, so only longer disagreements count.
        private const float BuffMismatchGrace = 0.5f;

        private readonly List<ZoneInfo> zones = new List<ZoneInfo>();
        private HUD hud;
        private ZoneHudPanel panel;
        private ZoneWorldRings rings;
        private GameObject cachedBodyObject;
        private CharacterBody cachedBody;
        private float buffMismatchTime;
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
            PageState actualPageState = ZoneScanner.GetPageState(body);
            PageState pageState = actualPageState;
            if (pageState == PageState.None && settings.DebugAlwaysShow.Value && body)
            {
                pageState = PageState.Usable;
            }
            bool holdsPage = pageState != PageState.None;
            if (holdsPage)
            {
                activeIndex = ZoneScanner.Scan(body, zones);
            }

            if (settings.DebugLogging.Value)
            {
                ZoneDebugDump.LogNewZones();
                ReportBuffMismatch(body, actualPageState == PageState.Usable, activeIndex >= 0);
            }

            if (!panel && hud.keyItemIcon)
            {
                panel = ZoneHudPanel.Create(hud, hud.keyItemIcon);
            }
            if (panel)
            {
                int availableZones = CountIndicatorZones();
                if (settings.ShowPanel.Value && activeIndex >= 0)
                {
                    string disabledCause = pageState == PageState.Disabled ? ZoneScanner.DescribeDisabledCause(body) : null;
                    panel.Show(zones[activeIndex], disabledCause);
                }
                else if (settings.ShowZoneIndicator.Value && availableZones > 0)
                {
                    panel.ShowIndicator(availableZones, pageState == PageState.Disabled);
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
            if (holdsPage && settings.ShowWorldRings.Value)
            {
                rings.UpdateRings(zones, body.corePosition, settings.RingMaxDistance.Value);
            }
            else
            {
                rings.Clear();
            }
        }

        // The stage-start reroll zone covers the whole map for its first 30 seconds, so it isn't worth flagging.
        private int CountIndicatorZones()
        {
            int count = 0;
            foreach (ZoneInfo zone in zones)
            {
                if (!zone.IsStageStart)
                {
                    count++;
                }
            }
            return count;
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
            bool hasBuff = usable && ZoneScanner.HasReadyBuff(body);
            if (!usable || hasBuff == inZone)
            {
                buffMismatchTime = 0f;
                reportedBuffMismatch = false;
                return;
            }

            buffMismatchTime += Time.deltaTime;
            if (buffMismatchTime >= BuffMismatchGrace && !reportedBuffMismatch)
            {
                reportedBuffMismatch = true;
                ReincarnationDisplayPlugin.Log.LogWarning(
                    $"In-zone mismatch: local check says {inZone}, server buff says {hasBuff} at {body.corePosition}");
            }
        }
    }
}
