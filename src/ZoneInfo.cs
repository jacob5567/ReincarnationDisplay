using RoR2;
using UnityEngine;

namespace ReincarnationDisplay
{
    internal enum PageState
    {
        // No Reincarnation page in the inventory (consumed copies don't count).
        None,
        Usable,
        // Held, but disabled, e.g. by Hex of Loss. Disabled items drop out of the effective item count.
        Disabled,
    }

    internal enum ZoneShape
    {
        Tube,
        Sphere,
        Box,
        // Covers the whole map: inverted zones (everything outside a shape) and huge ones like the stage-start zone.
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
