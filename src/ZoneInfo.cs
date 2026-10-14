using RoR2;
using UnityEngine;

namespace ReincarnationDisplay
{
    internal enum ZoneShape
    {
        Tube,
        Sphere,
        Box,
        // Inverted zones cover everything outside a shape; the stage-start zone looks like this.
        Everywhere,
        Unknown,
    }

    internal struct ZoneInfo
    {
        public CursedBookSacrificeZone Zone;
        public ZoneShape Shape;
        public Vector3 Center;
        // Tube and Sphere only.
        public float Radius;
        // Box only. World axis-aligned, matching BoxZone.IsInBounds.
        public Vector3 HalfExtents;
        public bool PlayerInside;
        public bool IsStageStart;
        // Seconds left in the stage-start window. Only meaningful when IsStageStart.
        public float WindowRemaining;
    }
}
