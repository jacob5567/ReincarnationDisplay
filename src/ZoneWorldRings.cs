using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReincarnationDisplay
{
    // Ground-hugging outlines marking the edge of each valid zone.
    internal sealed class ZoneWorldRings : MonoBehaviour
    {
        private const int CircleSegments = 64;
        private const int BoxSegmentsPerEdge = 16;
        private const float LineWidth = 0.25f;
        private const float InsideLineWidth = 0.4f;
        private const float GroundOffset = 0.2f;
        private const float RayStartHeight = 12f;
        private const float RayLength = 40f;
        private const float RebuildTolerance = 0.05f;

        private static readonly Color RingColor = new Color(0.76f, 0.55f, 1f, 0.45f);
        private static readonly Color InsideRingColor = new Color(0.86f, 0.66f, 1f, 0.95f);

        private sealed class Ring
        {
            public LineRenderer Line;
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
        private readonly Vector3[] points = new Vector3[Mathf.Max(CircleSegments, BoxSegmentsPerEdge * 4)];
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
                ring.Line.startColor = color;
                ring.Line.endColor = color;
                ring.Line.widthMultiplier = zone.PlayerInside ? InsideLineWidth : LineWidth;
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

            // Tubes are infinite columns, so their transform height may be nowhere near the floor; fall back to the viewer's.
            float referenceY = zone.Center.y;
            if (zone.Shape == ZoneShape.Tube && !TryGetGroundY(zone.Center.x, zone.Center.z, referenceY, out _))
            {
                referenceY = viewer.y;
            }

            int count = zone.Shape == ZoneShape.Box ? BuildBox(zone, referenceY) : BuildCircle(zone, referenceY);
            ring.Line.positionCount = count;
            ring.Line.SetPositions(points);
        }

        private int BuildCircle(ZoneInfo zone, float referenceY)
        {
            float radius = zone.Radius;
            // A sphere meets the ground in a smaller circle when its center is above the floor.
            if (zone.Shape == ZoneShape.Sphere && TryGetGroundY(zone.Center.x, zone.Center.z, referenceY, out float groundY))
            {
                float height = zone.Center.y - groundY;
                if (Mathf.Abs(height) < radius)
                {
                    radius = Mathf.Sqrt(radius * radius - height * height);
                    referenceY = groundY;
                }
            }

            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / CircleSegments);
                float x = zone.Center.x + Mathf.Cos(angle) * radius;
                float z = zone.Center.z + Mathf.Sin(angle) * radius;
                points[i] = new Vector3(x, GroundYOrDefault(x, z, referenceY), z);
            }
            return CircleSegments;
        }

        private int BuildBox(ZoneInfo zone, float referenceY)
        {
            Vector3 half = zone.HalfExtents;
            Vector2[] corners =
            {
                new Vector2(-half.x, -half.z), new Vector2(half.x, -half.z),
                new Vector2(half.x, half.z), new Vector2(-half.x, half.z),
            };
            int index = 0;
            for (int edge = 0; edge < 4; edge++)
            {
                Vector2 from = corners[edge];
                Vector2 to = corners[(edge + 1) % 4];
                for (int step = 0; step < BoxSegmentsPerEdge; step++)
                {
                    Vector2 local = Vector2.Lerp(from, to, step / (float)BoxSegmentsPerEdge);
                    float x = zone.Center.x + local.x;
                    float z = zone.Center.z + local.y;
                    points[index++] = new Vector3(x, GroundYOrDefault(x, z, referenceY), z);
                }
            }
            return index;
        }

        private static float GroundYOrDefault(float x, float z, float referenceY)
        {
            return (TryGetGroundY(x, z, referenceY, out float groundY) ? groundY : referenceY) + GroundOffset;
        }

        private static bool TryGetGroundY(float x, float z, float referenceY, out float groundY)
        {
            Vector3 origin = new Vector3(x, referenceY + RayStartHeight, z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RayLength, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                return true;
            }
            groundY = referenceY;
            return false;
        }

        private Ring Rent()
        {
            if (pool.Count > 0)
            {
                Ring pooled = pool.Pop();
                pooled.Built = false;
                pooled.Line.gameObject.SetActive(true);
                return pooled;
            }

            GameObject lineObject = new GameObject("Ring");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = true;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return new Ring { Line = line };
        }

        private void Release(Ring ring)
        {
            ring.Line.gameObject.SetActive(false);
            pool.Push(ring);
        }
    }
}
