using System.Collections.Generic;
using CombatExtended;
using RimWorld;
using Verse;

namespace CombatExtended.Compatibility.AnomalyPortalCompat;

// Port of AnomalyPortal_Library.Projectile_Teleport: applies CE damage, then teleports the hit pawn.
public class BulletCE_Teleport : BulletCE
{
    public float teleportChance = 0.5f;

    // Matches AnomalyPortal TeleportUtility.GetDestination radius.
    public float teleportRadius = 24.9f;

    // Minimum throw distance so the target does not land on the impact tile.
    private const float MinTeleportDistance = 6.9f;

    public override void Impact(Thing hitThing)
    {
        // Cache before base.Impact destroys this projectile.
        Map map = hitThing?.Map ?? base.Map;
        Pawn pawnTarget = hitThing as Pawn;

        base.Impact(hitThing);

        if (pawnTarget == null || pawnTarget.Destroyed || !pawnTarget.Spawned || map == null)
        {
            return;
        }

        if (!Rand.Chance(teleportChance))
        {
            return;
        }

        if (!TryGetTeleportDestination(pawnTarget.Position, map, out IntVec3 destination))
        {
            return;
        }

        pawnTarget.Position = destination;
        pawnTarget.Notify_Teleported();

        if (pawnTarget.Faction == Faction.OfPlayer)
        {
            FloodFillerFog.FloodUnfog(destination, map);
        }

        EffecterDefOf.Skip_Entry.SpawnMaintained(destination, map);
    }

    private bool TryGetTeleportDestination(IntVec3 center, Map map, out IntVec3 result)
    {
        List<IntVec3> candidates = new List<IntVec3>();
        int numCells = GenRadial.NumCellsInRadius(teleportRadius);
        for (int i = 0; i < numCells; i++)
        {
            IntVec3 offset = GenRadial.RadialPattern[i];
            if (offset.DistanceTo(IntVec3.Zero) < MinTeleportDistance)
            {
                continue;
            }
            IntVec3 candidate = center + offset;
            if (candidate.InBounds(map) && candidate.Standable(map) && !candidate.Fogged(map))
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            result = IntVec3.Invalid;
            return false;
        }

        result = candidates.RandomElement();
        return true;
    }
}
