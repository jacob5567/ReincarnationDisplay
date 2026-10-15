using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReincarnationDisplay
{
    // Outlines that follow the floor along the edge of each valid zone. Where the outline would jump onto a rock or
    // hang over a drop, that stretch is left out instead of drawing a spike.
    internal sealed class ZoneWorldRings : MonoBehaviour
    {
        private const float LineWidth = 0.25f;
        private const float InsideLineWidth = 0.4f;
        private const float GroundOffset = 0.2f;
        private const float RebuildTolerance = 0.05f;

        // Distance between outline samples, and limits on how many a ring gets.
        private const float SampleSpacing = 1f;
        private const int MinSamples = 32;
        private const int MaxSamples = 512;
        // Each sample looks for the floor from this far above the zone's floor level down to this far below it, so
        // zones on slopes still get an outline. No floor in that range is an air gap; rocks and ledges inside it are
        // caught by the height check between neighbors instead.
        private const float RayAbove = 15f;
        private const float RayBelow = 30f;
        // Neighboring samples further apart in height than this (plus slope over their spacing) are a rock face or
        // ledge, so they aren't joined.
        private const float MaxStepHeight = 0.75f;
        private const float MaxSlope = 1.2f;
        // Pieces with fewer samples than this are dropped, so rock tops don't leave stray dashes.
        private const int MinPieceSamples = 3;
        // How far above and below a zone's origin to look for its floor level.
        private const float FloorSearchReach = 20f;
        // The downward floor search starts this far above the origin, so an origin sitting just under the floor
        // surface still finds it.
        private const float FloorSearchLift = 1f;

        private static readonly Color RingColor = new Color(0.76f, 0.55f, 1f, 0.45f);
        private static readonly Color InsideRingColor = new Color(0.86f, 0.66f, 1f, 0.95f);

        private sealed class Ring
        {
            public GameObject Root;
            // One line per unbroken piece of the outline; only the first ActiveLines are in use.
            public readonly List<LineRenderer> Lines = new List<LineRenderer>();
            public int ActiveLines;
            public ZoneShape Shape;
            public Vector3 Center;
            public float Radius;
            public Vector3 HalfExtents;
            public bool Built;
            public bool Seen;
        }

        private readonly Dictionary<CursedBookSacrificeZone, Ring> rings = new Dictionary<CursedBookSacrificeZone, Ring>();
        private readonly Stack<Ring> pool = new Stack<Ring>();
        private readonly List<CursedBookSacrificeZone> staleZones = new List<CursedBookSacrificeZone>();
        private readonly Vector3[] samples = new Vector3[MaxSamples];
        private readonly bool[] sampleOnFloor = new bool[MaxSamples];
        private readonly List<Vector3> piece = new List<Vector3>();
        private Material material;

        public static ZoneWorldRings Create()
        {
            return new GameObject("ReincarnationZoneRings").AddComponent<ZoneWorldRings>();
        }

        private void Awake()
        {
            Material template = GetDefaultSpriteMaterial();
            if (!template)
            {
                ReincarnationDisplayPlugin.Log.LogError("No usable line material found; world rings are disabled.");
                return;
            }
            material = new Material(template);
        }

        private void OnDestroy()
        {
            if (material)
            {
                Destroy(material);
            }
        }

        // RoR2BepInExPack reroutes Shader.Find through the game's shader table, which throws for built-in names like
        // "Sprites/Default". A new SpriteRenderer is given Unity's built-in Sprites-Default material instead, which
        // tints by vertex color, the way ring colors are applied.
        private static Material GetDefaultSpriteMaterial()
        {
            GameObject probe = new GameObject("ReincarnationMaterialProbe");
            try
            {
                return probe.AddComponent<SpriteRenderer>().sharedMaterial;
            }
            finally
            {
                DestroyImmediate(probe);
            }
        }

        public void Clear()
        {
            if (rings.Count == 0)
            {
                return;
            }
            foreach (Ring ring in rings.Values)
            {
                Release(ring);
            }
            rings.Clear();
        }

        public void UpdateRings(List<ZoneInfo> zones, Vector3 viewer, float maxDistance)
        {
            if (!material)
            {
                return;
            }
            foreach (Ring ring in rings.Values)
            {
                ring.Seen = false;
            }

            foreach (ZoneInfo zone in zones)
            {
                if (zone.Shape != ZoneShape.Tube && zone.Shape != ZoneShape.Sphere && zone.Shape != ZoneShape.Box)
                {
                    continue;
                }
                if (DistanceToEdge(zone, viewer) > maxDistance)
                {
                    continue;
                }

                if (!rings.TryGetValue(zone.Zone, out Ring ring))
                {
                    ring = Rent();
                    rings.Add(zone.Zone, ring);
                }
                ring.Seen = true;
                if (NeedsRebuild(ring, zone))
                {
                    Rebuild(ring, zone, viewer);
                }

                Color color = zone.PlayerInside ? InsideRingColor : RingColor;
                float width = zone.PlayerInside ? InsideLineWidth : LineWidth;
                for (int i = 0; i < ring.ActiveLines; i++)
                {
                    LineRenderer line = ring.Lines[i];
                    line.startColor = color;
                    line.endColor = color;
                    line.widthMultiplier = width;
                }
            }

            foreach (KeyValuePair<CursedBookSacrificeZone, Ring> entry in rings)
            {
                if (!entry.Value.Seen)
                {
                    staleZones.Add(entry.Key);
                }
            }
            foreach (CursedBookSacrificeZone zone in staleZones)
            {
                Release(rings[zone]);
                rings.Remove(zone);
            }
            staleZones.Clear();
        }

        private static float DistanceToEdge(ZoneInfo zone, Vector3 viewer)
        {
            Vector2 offset = new Vector2(viewer.x - zone.Center.x, viewer.z - zone.Center.z);
            if (zone.Shape == ZoneShape.Box)
            {
                Vector2 outside = new Vector2(
                    Mathf.Max(0f, Mathf.Abs(offset.x) - zone.HalfExtents.x),
                    Mathf.Max(0f, Mathf.Abs(offset.y) - zone.HalfExtents.z));
                return outside.magnitude;
            }
            return Mathf.Max(0f, offset.magnitude - zone.Radius);
        }

        private static bool NeedsRebuild(Ring ring, ZoneInfo zone)
        {
            return !ring.Built
                || ring.Shape != zone.Shape
                || (ring.Center - zone.Center).sqrMagnitude > RebuildTolerance * RebuildTolerance
                || Mathf.Abs(ring.Radius - zone.Radius) > RebuildTolerance
                || (ring.HalfExtents - zone.HalfExtents).sqrMagnitude > RebuildTolerance * RebuildTolerance;
        }

        private void Rebuild(Ring ring, ZoneInfo zone, Vector3 viewer)
        {
            ring.Built = true;
            ring.Shape = zone.Shape;
            ring.Center = zone.Center;
            ring.Radius = zone.Radius;
            ring.HalfExtents = zone.HalfExtents;

            int count = zone.Shape == ZoneShape.Box ? SampleBox(zone) : SampleCircle(zone, viewer);
            ring.ActiveLines = 0;
            EmitPieces(ring, count);
            for (int i = ring.ActiveLines; i < ring.Lines.Count; i++)
            {
                ring.Lines[i].gameObject.SetActive(false);
            }
        }

        // Samples the circle where the zone meets its floor level, each dropped onto the floor below it.
        private int SampleCircle(ZoneInfo zone, Vector3 viewer)
        {
            float radius = zone.Radius;
            float floorY;
            if (zone.Shape == ZoneShape.Sphere)
            {
                // The sphere's cross-section at the floor under its center: the zone's edge at floor level.
                floorY = TryGetGroundY(zone.Center, Mathf.Max(radius, FloorSearchReach), out float groundY) ? groundY : zone.Center.y;
                float height = zone.Center.y - floorY;
                radius = Mathf.Sqrt(Mathf.Max(0f, radius * radius - height * height));
            }
            else
            {
                // Tubes are infinite columns, so their transform height may be nowhere near the floor.
                floorY = TryGetGroundY(zone.Center, FloorSearchReach, out float groundY) ? groundY : viewer.y;
            }

            int count = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * radius / SampleSpacing), MinSamples, MaxSamples);
            for (int i = 0; i < count; i++)
            {
                float angle = i * (2f * Mathf.PI / count);
                SampleAt(i, zone.Center.x + Mathf.Cos(angle) * radius, zone.Center.z + Mathf.Sin(angle) * radius, floorY);
            }
            return count;
        }

        private int SampleBox(ZoneInfo zone)
        {
            Vector3 half = zone.HalfExtents;
            float floorY = TryGetGroundY(zone.Center, Mathf.Max(half.y, FloorSearchReach), out float groundY) ? groundY : zone.Center.y;
            Vector2[] corners =
            {
                new Vector2(-half.x, -half.z), new Vector2(half.x, -half.z),
                new Vector2(half.x, half.z), new Vector2(-half.x, half.z),
            };

            float perimeter = 4f * (half.x + half.z);
            int count = Mathf.Clamp(Mathf.CeilToInt(perimeter / SampleSpacing), MinSamples, MaxSamples);
            int index = 0;
            for (int edge = 0; edge < 4; edge++)
            {
                Vector2 from = corners[edge];
                Vector2 to = corners[(edge + 1) % 4];
                int edgeSamples = edge < 3
                    ? Mathf.Max(1, Mathf.RoundToInt(count * (to - from).magnitude / perimeter))
                    : count - index;
                for (int step = 0; step < edgeSamples && index < count; step++)
                {
                    Vector2 local = Vector2.Lerp(from, to, step / (float)edgeSamples);
                    SampleAt(index++, zone.Center.x + local.x, zone.Center.z + local.y, floorY);
                }
            }
            return index;
        }

        private void SampleAt(int index, float x, float z, float floorY)
        {
            Vector3 origin = new Vector3(x, floorY + RayAbove, z);
            bool onFloor = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RayAbove + RayBelow,
                LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            samples[index] = new Vector3(x, (onFloor ? hit.point.y : floorY) + GroundOffset, z);
            sampleOnFloor[index] = onFloor;
        }

        // Splits the closed outline wherever a sample found no floor or two neighbors are too far apart in height,
        // then draws each remaining piece as its own line.
        private void EmitPieces(Ring ring, int count)
        {
            int firstBreak = -1;
            for (int i = 0; i < count; i++)
            {
                if (!Joined(i, (i + 1) % count))
                {
                    firstBreak = i;
                    break;
                }
            }

            if (firstBreak < 0)
            {
                piece.Clear();
                for (int i = 0; i < count; i++)
                {
                    piece.Add(samples[i]);
                }
                AddLine(ring, true);
                return;
            }

            // Start just after a break so no piece wraps around the end of the array.
            piece.Clear();
            for (int step = 1; step <= count; step++)
            {
                int i = (firstBreak + step) % count;
                if (sampleOnFloor[i])
                {
                    piece.Add(samples[i]);
                }
                if (!Joined(i, (i + 1) % count))
                {
                    if (piece.Count >= MinPieceSamples)
                    {
                        AddLine(ring, false);
                    }
                    piece.Clear();
                }
            }
        }

        private bool Joined(int a, int b)
        {
            if (!sampleOnFloor[a] || !sampleOnFloor[b])
            {
                return false;
            }
            Vector3 pa = samples[a];
            Vector3 pb = samples[b];
            float spacing = new Vector2(pb.x - pa.x, pb.z - pa.z).magnitude;
            return Mathf.Abs(pb.y - pa.y) <= MaxStepHeight + spacing * MaxSlope;
        }

        private void AddLine(Ring ring, bool closed)
        {
            LineRenderer line;
            if (ring.ActiveLines < ring.Lines.Count)
            {
                line = ring.Lines[ring.ActiveLines];
            }
            else
            {
                line = CreateLine(ring.Root.transform);
                ring.Lines.Add(line);
            }
            ring.ActiveLines++;

            line.gameObject.SetActive(true);
            line.loop = closed;
            line.positionCount = piece.Count;
            line.SetPositions(piece.ToArray());
        }

        // Floor under a point, within reach below it. Only if there is none does it look above, for zones whose origin
        // is buried; looking from above first would land on any roof, bridge or overhang over the zone.
        private static bool TryGetGroundY(Vector3 center, float reach, out float groundY)
        {
            Vector3 below = center + Vector3.up * FloorSearchLift;
            if (Physics.Raycast(below, Vector3.down, out RaycastHit hit, reach + FloorSearchLift, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)
                || Physics.Raycast(center + Vector3.up * reach, Vector3.down, out hit, reach - FloorSearchLift, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                return true;
            }
            groundY = center.y;
            return false;
        }

        private Ring Rent()
        {
            if (pool.Count > 0)
            {
                Ring pooled = pool.Pop();
                pooled.Built = false;
                pooled.Root.SetActive(true);
                return pooled;
            }

            GameObject root = new GameObject("Ring");
            root.transform.SetParent(transform, false);
            return new Ring { Root = root };
        }

        private LineRenderer CreateLine(Transform parent)
        {
            GameObject lineObject = new GameObject("Piece");
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Release(Ring ring)
        {
            ring.Root.SetActive(false);
            pool.Push(ring);
        }
    }
}
