using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using RoR2;
using UnityEngine;

namespace ReincarnationDisplay
{
    // Logs everything known about each sacrifice zone, to confirm how zones are configured in game.
    internal static class ZoneDebugDump
    {
        private static readonly HashSet<int> loggedZoneIds = new HashSet<int>();

        public static void LogNewZones()
        {
            foreach (CursedBookSacrificeZone zone in CursedBookSacrificeZone.instanceList)
            {
                if (zone && loggedZoneIds.Add(zone.GetInstanceID()))
                {
                    LogZone(zone, "New sacrifice zone");
                }
            }
        }

        public static void DumpAll()
        {
            float stageAge = Stage.instance ? Stage.instance.entryTime.timeSince : -1f;
            ReincarnationDisplayPlugin.Log.LogInfo(
                $"=== Sacrifice zone dump: scene={SceneCatalog.mostRecentSceneDef?.cachedName} stageAge={stageAge:0.00}s " +
                $"zones={CursedBookSacrificeZone.instanceList.Count} ===");

            foreach (CursedBookSacrificeZone zone in CursedBookSacrificeZone.instanceList)
            {
                if (zone)
                {
                    LogZone(zone, "Sacrifice zone");
                }
            }

            DLC4SacrificeZoneSpawner spawner = DLC4SacrificeZoneSpawner.instance;
            GameObject prefab = spawner && spawner.reincarnateCard ? spawner.reincarnateCard.prefab : null;
            if (prefab)
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine($"Stage-start spawn card prefab '{prefab.name}':");
                AppendComponents(builder, prefab);
                ReincarnationDisplayPlugin.Log.LogInfo(builder.ToString());
            }
            else
            {
                ReincarnationDisplayPlugin.Log.LogInfo("No DLC4SacrificeZoneSpawner in this scene.");
            }
        }

        private static void LogZone(CursedBookSacrificeZone zone, string title)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"{title} '{GetPath(zone.transform)}' active={zone.gameObject.activeInHierarchy} position={zone.transform.position}");
            builder.AppendLine($"  behaviour={zone.behaviour} isDisabled={zone.isDisabled} " +
                               $"runFlag={zone.runFlagCondition}:'{zone.runFlagName}'={zone.runFlagConditionResult} pageRequirement={zone.pageRequirement}");
            builder.AppendLine($"  reroll={zone.useRerollSceneInstead} next={zone.useNextSceneInstead} explicit={zone.useExplicitSceneInstead} " +
                               $"explicitScene={zone.explicitScene?.cachedName} sceneCollection={zone.sceneCollection?.name} " +
                               $"sceneTierCollection={zone.sceneTierCollection?.name} doNotIncrementStageNumber={zone.doNotIncrementStageNumber}");
            builder.AppendLine($"  shape={DescribeShape(zone.zone)}");
            if (Run.instance)
            {
                string outcome = ZoneScanner.DescribeOutcome(zone, out bool keepsStageCount);
                builder.AppendLine($"  outcome='{outcome}' keepsStageCount={keepsStageCount}");
            }
            AppendComponents(builder, zone.gameObject);
            ReincarnationDisplayPlugin.Log.LogInfo(builder.ToString());
        }

        private static string DescribeShape(IZone zone)
        {
            switch (zone)
            {
                case VerticalTubeZone tube:
                    return $"VerticalTubeZone radius={tube.radius} enabled={tube.enabled}";
                case SphereZone sphere:
                    return $"SphereZone radius={sphere.radius} inverted={sphere.isInverted} enabled={sphere.enabled}";
                case BoxZone box:
                    return $"BoxZone halfExtents={box.transform.lossyScale * 0.5f} inverted={box.isInverted} enabled={box.enabled}";
                case null:
                    return "none";
                default:
                    return zone.GetType().FullName;
            }
        }

        // Every component in the hierarchy, with the simple serialized fields of game-defined ones (timers, flags, etc).
        private static void AppendComponents(StringBuilder builder, GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (!component)
                {
                    continue;
                }
                Type type = component.GetType();
                builder.Append($"  [{GetPath(component.transform)}] {type.FullName}");
                if (component is Behaviour behaviour)
                {
                    builder.Append(behaviour.enabled ? "" : " (disabled)");
                }
                builder.AppendLine();

                if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
                {
                    continue;
                }
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    bool serialized = field.IsPublic || field.IsDefined(typeof(SerializeField), false);
                    Type fieldType = field.FieldType;
                    if (serialized && (fieldType.IsPrimitive || fieldType.IsEnum || fieldType == typeof(string)))
                    {
                        builder.AppendLine($"      {field.Name}={field.GetValue(component)}");
                    }
                }
            }
        }

        private static string GetPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }
            return path;
        }
    }
}
