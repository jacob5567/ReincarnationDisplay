using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ReincarnationDisplay
{
    // Mirrors CursedBookSacrificeZone.IsValidForPlayer without calling it. The game's version goes through
    // GetNextScene, which is server-only and, on the host, draws from Run.nextStageRng, so calling it from
    // here would change which stage the run picks. Everything below only reads state.
    internal static class ZoneScanner
    {
        private const int ChampRushPageRequirement = 3;
        private const int ProvidencePageRequirement = 6;
        // The stage-start zone is a plain SphereZone with a 65536 m radius rather than an inverted one.
        private const float EverywhereRadius = 10000f;

        private static readonly List<SceneDef> rerollCandidates = new List<SceneDef>();
        private static SceneDef rerollCandidatesScene;
        // Enabled expansions can differ between runs.
        private static Run rerollCandidatesRun;

        private static readonly List<SceneDef> sceneBuffer = new List<SceneDef>();

        public static PageState GetPageState(CharacterBody body)
        {
            ItemDef page = DLC4Content.Items.PageDie;
            if (!body || !Run.instance || !page)
            {
                return PageState.None;
            }
            if (!body.healthComponent || !body.healthComponent.alive)
            {
                return PageState.None;
            }
            Inventory inventory = body.inventory;
            if (!inventory || inventory.GetItemCountTotal(page) <= 0)
            {
                return PageState.None;
            }
            return inventory.GetItemCountEffective(page) > 0 ? PageState.Usable : PageState.Disabled;
        }

        // Why a held page is disabled, for the panel. Hex of Loss is the usual cause.
        public static string DescribeDisabledCause(CharacterBody body)
        {
            ItemDef hex = DLC4Content.Items.RoamingItemDisable;
            Inventory inventory = body ? body.inventory : null;
            if (hex && inventory && inventory.GetItemCountTotal(hex) > 0)
            {
                return "Disabled by " + Language.GetString(hex.nameToken);
            }
            return "Page is disabled";
        }

        public static bool HasReadyBuff(CharacterBody body)
        {
            BuffDef buff = DLC4Content.Buffs.CursedBookReadyForSacrifice;
            return buff && body && body.HasBuff(buff);
        }

        // Fills results with every currently valid zone, in instanceList order. Returns the index of the zone a
        // cast at the body's position would use (the first valid one containing it, as FindSacrificeZone does), or -1.
        public static int Scan(CharacterBody body, List<ZoneInfo> results)
        {
            int teamPageCount = Util.GetTotalPageCountFromTeam(TeamIndex.Player, true, true);
            Vector3 position = body.corePosition;
            int activeIndex = -1;

            foreach (CursedBookSacrificeZone zone in CursedBookSacrificeZone.instanceList)
            {
                if (!IsValid(zone, teamPageCount))
                {
                    continue;
                }

                ZoneInfo info = Describe(zone);
                if (info.IsStageStart && info.WindowRemaining <= 0f
                    && ReincarnationDisplayPlugin.Settings.HideStageStartZoneAfterWindow.Value)
                {
                    continue;
                }

                info.PlayerInside = zone.zone.IsInBounds(position);
                if (info.PlayerInside && activeIndex < 0)
                {
                    activeIndex = results.Count;
                }
                results.Add(info);
            }
            return activeIndex;
        }

        private static bool IsValid(CursedBookSacrificeZone zone, int teamPageCount)
        {
            if (!zone || !zone.gameObject.activeInHierarchy)
            {
                return false;
            }
            if (zone.isDisabled || !zone.runFlagConditionResult)
            {
                return false;
            }
            if (!(zone.zone is Object zoneObject) || !zoneObject)
            {
                return false;
            }
            if (zone.behaviour == CursedBookSacrificeZone.Behaviour.KillAndLoad && !HasPossibleDestination(zone))
            {
                return false;
            }
            return teamPageCount >= RequiredPageCount(zone.pageRequirement);
        }

        private static int RequiredPageCount(CursedBookSacrificeZone.PageRequirements requirement)
        {
            switch (requirement)
            {
                case CursedBookSacrificeZone.PageRequirements.ChampRushMinimum:
                    return Util.ScalePageRequirement(ChampRushPageRequirement);
                case CursedBookSacrificeZone.PageRequirements.ProvidenceMinimum:
                    return Util.ScalePageRequirement(ProvidencePageRequirement);
                default:
                    return 0;
            }
        }

        // Same branch order as GetNextScene, but only asks whether a destination exists.
        private static bool HasPossibleDestination(CursedBookSacrificeZone zone)
        {
            if (zone.useRerollSceneInstead)
            {
                return GetRerollCandidates().Count > 0;
            }
            if (zone.useNextSceneInstead)
            {
                // nextStageScene is not synced, so clients assume the server has one.
                return !NetworkServer.active || Run.instance.nextStageScene;
            }
            if (!zone.useExplicitSceneInstead)
            {
                GetCollectionCandidates(zone, sceneBuffer);
                return sceneBuffer.Count > 0;
            }
            return zone.explicitScene && CanPickStage(zone.explicitScene);
        }

        private static ZoneInfo Describe(CursedBookSacrificeZone zone)
        {
            ZoneInfo info = new ZoneInfo { Zone = zone, Shape = ZoneShape.Unknown, Center = zone.transform.position };

            switch (zone.zone)
            {
                case VerticalTubeZone tube:
                    info.Shape = tube.radius >= EverywhereRadius ? ZoneShape.Everywhere : ZoneShape.Tube;
                    info.Center = tube.transform.position;
                    info.Radius = tube.radius;
                    break;
                case SphereZone sphere:
                    info.Shape = sphere.isInverted || sphere.radius >= EverywhereRadius ? ZoneShape.Everywhere : ZoneShape.Sphere;
                    info.Center = sphere.transform.position;
                    info.Radius = sphere.radius;
                    break;
                case BoxZone box:
                    info.Shape = box.isInverted ? ZoneShape.Everywhere : ZoneShape.Box;
                    info.Center = box.transform.position;
                    info.HalfExtents = box.transform.lossyScale * 0.5f;
                    break;
            }

            info.IsStageStart = IsStageStartZone(zone);
            if (info.IsStageStart)
            {
                info.WindowRemaining = GetWindowRemaining(zone);
            }
            return info;
        }

        // The stage-start zone carries an EntityLogic.Timer (30 s) that removes it when it fires. It counts up on
        // game time and stops once fired, so it also pauses with the game. Without one, count from stage entry.
        private static float GetWindowRemaining(CursedBookSacrificeZone zone)
        {
            float sinceEntry = Stage.instance ? Stage.instance.entryTime.timeSince : 0f;
            RoR2.EntityLogic.Timer timer = zone.GetComponent<RoR2.EntityLogic.Timer>();
            if (!timer)
            {
                return ReincarnationDisplayPlugin.Settings.StageStartWindowSeconds.Value - sinceEntry;
            }

            float remaining = timer.timerRunning ? timer.duration - timer.stopwatch : 0f;
            // The timer isn't networked. A client's copy starts when the zone spawns there, which can be seconds after
            // the server's, and only the server's closes the window. Stage entry time is synced, so cap by it.
            if (!NetworkServer.active && Stage.instance)
            {
                remaining = Mathf.Min(remaining, timer.duration - sinceEntry);
            }
            return remaining;
        }

        // The stage-start zone is the one DLC4SacrificeZoneSpawner spawns from reincarnateCard: a reroll zone. No other
        // zone is known to reroll, so any reroll zone is treated as the stage-start one, countdown included.
        private static bool IsStageStartZone(CursedBookSacrificeZone zone)
        {
            if (zone.useRerollSceneInstead)
            {
                return true;
            }
            DLC4SacrificeZoneSpawner spawner = DLC4SacrificeZoneSpawner.instance;
            GameObject prefab = spawner && spawner.reincarnateCard ? spawner.reincarnateCard.prefab : null;
            return prefab && zone.gameObject.name.StartsWith(prefab.name);
        }

        // One line saying what a sacrifice in this zone does, e.g. "Reroll stage 3" or "Destination: Sulfur Pools".
        // Only guaranteed destinations are named. When the game picks the stage (the run's next stage, or a random
        // entry from a collection), the player wouldn't otherwise know it, so the line doesn't reveal it.
        public static string DescribeOutcome(CursedBookSacrificeZone zone, out bool keepsStageCount)
        {
            keepsStageCount = zone.ShouldPreventStageCounterIncrement;

            if (zone.behaviour == CursedBookSacrificeZone.Behaviour.OnlyTriggerEvent)
            {
                keepsStageCount = false;
                return "Opens the Veil of Memory";
            }

            if (zone.useRerollSceneInstead)
            {
                // A reroll never advances the stage count, so the note would only repeat the line.
                keepsStageCount = false;
                return "Reroll stage " + (Run.instance.stageClearCount + 1);
            }
            if (zone.useNextSceneInstead)
            {
                return "Continues to the next stage";
            }
            if (!zone.useExplicitSceneInstead)
            {
                GetCollectionCandidates(zone, sceneBuffer);
                if (sceneBuffer.Count == 1)
                {
                    return "Destination: " + SceneName(sceneBuffer[0]);
                }
                if (zone.sceneTierCollection)
                {
                    return "Continues to the next stage";
                }
                // Trial zones (e.g. Sunken Hamlet) pick a random Winnowing trial from DLC4TrialCollection; don't say which.
                return zone.sceneCollection && zone.sceneCollection.name.Contains("Trial")
                    ? "Enters a random Winnowing trial"
                    : "Continues to a random stage";
            }
            return zone.explicitScene ? "Destination: " + SceneName(zone.explicitScene) : "Unknown destination";
        }

        // Scenes with the current scene's stage order, other than the current scene (GetNextScene's reroll filter).
        private static List<SceneDef> GetRerollCandidates()
        {
            SceneDef current = SceneCatalog.mostRecentSceneDef;
            if (current == rerollCandidatesScene && Run.instance == rerollCandidatesRun)
            {
                return rerollCandidates;
            }

            rerollCandidates.Clear();
            rerollCandidatesScene = current;
            rerollCandidatesRun = Run.instance;
            if (!current)
            {
                return rerollCandidates;
            }
            foreach (SceneDef def in SceneCatalog.allSceneDefs)
            {
                if (def && def != current && def.stageOrder == current.stageOrder && CanPickStage(def))
                {
                    rerollCandidates.Add(def);
                }
            }
            return rerollCandidates;
        }

        // The entries GetNextScene's weighted pick could land on, including the looped-variant swap.
        internal static void GetCollectionCandidates(CursedBookSacrificeZone zone, List<SceneDef> results)
        {
            results.Clear();
            Run run = Run.instance;
            SceneCollection collection = zone.sceneTierCollection
                ? zone.sceneTierCollection.GetSceneCollection(run.stageClearCountInCurrentLoop + 1)
                : zone.sceneCollection;
            if (!collection)
            {
                return;
            }

            foreach (SceneCollection.SceneEntry entry in collection.sceneEntries)
            {
                SceneDef def = entry.sceneDef;
                if (!def)
                {
                    continue;
                }
                if (!CanPickStage(def))
                {
                    def = entry.fallbackSceneDef;
                    if (!def)
                    {
                        continue;
                    }
                }
                if (def.loopedSceneDef && run.loopClearCount > 0)
                {
                    def = def.loopedSceneDef;
                }
                if (!results.Contains(def))
                {
                    results.Add(def);
                }
            }
        }

        private static bool CanPickStage(SceneDef def)
        {
            return !def.requiredExpansion || Run.instance.IsExpansionEnabled(def.requiredExpansion);
        }

        internal static string SceneName(SceneDef def)
        {
            string name = Language.GetString(def.nameToken);
            return string.IsNullOrEmpty(name) || name == def.nameToken ? def.cachedName : name;
        }
    }
}
