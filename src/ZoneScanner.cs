using System.Collections.Generic;
using System.Text;
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

        private static readonly List<SceneDef> rerollCandidates = new List<SceneDef>();
        private static SceneDef rerollCandidatesScene;
        // Enabled expansions can differ between runs.
        private static Run rerollCandidatesRun;

        private static readonly List<SceneDef> sceneBuffer = new List<SceneDef>();
        private static readonly StringBuilder textBuilder = new StringBuilder();

        public static bool IsPageUsable(CharacterBody body)
        {
            if (!body || !Run.instance || !DLC4Content.Items.PageDie)
            {
                return false;
            }
            if (!body.healthComponent || !body.healthComponent.alive)
            {
                return false;
            }
            Inventory inventory = body.inventory;
            return inventory && inventory.GetItemCountEffective(DLC4Content.Items.PageDie) > 0;
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
                    info.Shape = ZoneShape.Tube;
                    info.Center = tube.transform.position;
                    info.Radius = tube.radius;
                    break;
                case SphereZone sphere:
                    info.Shape = sphere.isInverted ? ZoneShape.Everywhere : ZoneShape.Sphere;
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
                float elapsed = Stage.instance ? Stage.instance.entryTime.timeSince : 0f;
                info.WindowRemaining = ReincarnationDisplayPlugin.Settings.StageStartWindowSeconds.Value - elapsed;
            }
            return info;
        }

        // The stage-start zone is the one DLC4SacrificeZoneSpawner spawns from reincarnateCard: a reroll zone.
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

        public static string DescribeDestination(CursedBookSacrificeZone zone, out bool keepsStageCount)
        {
            keepsStageCount = zone.ShouldPreventStageCounterIncrement;

            if (zone.behaviour == CursedBookSacrificeZone.Behaviour.OnlyTriggerEvent)
            {
                keepsStageCount = false;
                return "Opens the Veil of Memory";
            }

            Run run = Run.instance;
            if (zone.useRerollSceneInstead)
            {
                return "Random stage " + (run.stageClearCount + 1) + ": " + JoinSceneNames(GetRerollCandidates());
            }
            if (zone.useNextSceneInstead)
            {
                SceneDef next = NetworkServer.active ? run.nextStageScene : null;
                return next ? SceneName(next) : "Next stage (stage " + (run.stageClearCount + 2) + ")";
            }
            if (!zone.useExplicitSceneInstead)
            {
                GetCollectionCandidates(zone, sceneBuffer);
                return sceneBuffer.Count == 1 ? SceneName(sceneBuffer[0]) : "One of: " + JoinSceneNames(sceneBuffer);
            }
            return zone.explicitScene ? SceneName(zone.explicitScene) : "Unknown destination";
        }

        // Scenes with the current scene's stage order, other than the current scene (GetNextScene's reroll filter).
        private static List<SceneDef> GetRerollCandidates()
        {
            SceneDef current = SceneCatalog.mostRecentSceneDef;
            if (current == rerollCandidatesScene && Run.instance == rerollCandidatesRun && rerollCandidates.Count > 0)
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
        private static void GetCollectionCandidates(CursedBookSacrificeZone zone, List<SceneDef> results)
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

        private static string JoinSceneNames(List<SceneDef> scenes)
        {
            if (scenes.Count == 0)
            {
                return "none available";
            }
            textBuilder.Clear();
            for (int i = 0; i < scenes.Count; i++)
            {
                if (i > 0)
                {
                    textBuilder.Append(" / ");
                }
                textBuilder.Append(SceneName(scenes[i]));
            }
            return textBuilder.ToString();
        }

        private static string SceneName(SceneDef def)
        {
            string name = Language.GetString(def.nameToken);
            return string.IsNullOrEmpty(name) || name == def.nameToken ? def.cachedName : name;
        }
    }
}
