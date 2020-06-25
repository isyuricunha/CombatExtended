using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CombatExtended.CombatExtended.LoggerUtils;
using UnityEngine;
using Verse;
using VFESecurity;

namespace CombatExtended.Compatibility
{
    public class VanillaFurnitureExpandedShields
    {
        private static int lastCacheTick = 0;
        private static HashSet<Building_Shield> shields;
        private const string VFES_ModName = "Vanilla Furniture Expanded - Security";

        private static readonly MethodInfo CanFunctionPropertyGetter = typeof(Building_Shield)?.GetProperty("CanFunction", BindingFlags.Instance | BindingFlags.NonPublic)?.GetGetMethod(nonPublic: true);
        public static bool CanInstall()
        {
            if (!ModLister.HasActiveModWithName(VFES_ModName))
            {
                return false;
            }

            return true;
        }
        public static void Install()
        {
            BlockerRegistry.RegisterCheckForCollisionCallback(CheckCollision);
            BlockerRegistry.RegisterImpactSomethingCallback(ImpactSomething);
        }

        private static bool CheckCollision(ProjectileCE projectile, IntVec3 cell, Thing launcher)
        {
            if (projectile.def.projectile.flyOverhead)
            {
                // All VFE shields are bullet shields. Don't block flying projectiles.
                return false;
            }

            var map = projectile.Map;
            Vector3 exactPosition = projectile.ExactPosition;

            refreshShields(map);

            foreach (var shield in shields)
            {
                if (!ShieldInterceptsProjectile(shield, projectile, launcher))
                {
                    continue;
                }

                if (!(projectile is ProjectileCE_Explosive))
                {
                    shield.AbsorbDamage(projectile.def.projectile.GetDamageAmount(launcher), projectile.def.projectile.damageDef, projectile.ExactRotation.eulerAngles.y);
                }
                projectile.ExactPosition = cell.ToVector3();
                return true;

            }
            return false;
        }

        private static bool ImpactSomething(ProjectileCE projectile, Thing launcher)
        {
            var map = projectile.Map;
            Vector3 exactPosition = projectile.ExactPosition;

            refreshShields(map);

            var blocked = shields.Any(shield => ShieldInterceptsProjectile(shield, projectile, launcher));
            CELogger.Message($"Blocked {projectile}? -- {blocked}");
            return blocked;
        }

        private static bool ShieldInterceptsProjectile(Building_Shield shield, ProjectileCE projectile, Thing launcher)
        {
            if (!shield.active && !(bool)CanFunctionPropertyGetter.Invoke(shield, null))
            {
                // Shield inactive, don't intercept.
                return false;
            }

            var shieldRadiusSquared = shield.ShieldRadius * shield.ShieldRadius;
            if (launcher.Position.DistanceToSquared(shield.Position) <= shieldRadiusSquared)
            {
                // Launcher inside shield, don't intercept.
                return false;
            }
            if (projectile.Position.DistanceToSquared(shield.Position) <= shieldRadiusSquared)
            {
                // Launcher outside, projectile inside, intercept.
                return true;
            }
            return false;
        }

        private static void refreshShields(Map map)
        {
            int thisTick = Find.TickManager.TicksAbs;
            if (lastCacheTick != thisTick)
            {
                shields = map.listerBuildings.AllBuildingsColonistOfClass<Building_Shield>().ToHashSet();
                lastCacheTick = thisTick;
            }
        }
    }
}