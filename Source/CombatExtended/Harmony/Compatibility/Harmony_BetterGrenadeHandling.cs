using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace CombatExtended.HarmonyCE.Compatibility;

public class Harmony_BetterGrenadeHandling
{
    private static Type TypeOfBGHUtils
    {
        get
        {
            return AccessTools.TypeByName("BetterGrenadeHandling.BGHUtils");
        }
    }

    [HarmonyPatch]
    public static class Harmony_ShouldBeHitByEMP
    {
        public static bool Prepare()
        {
            return TypeOfBGHUtils != null;
        }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(TypeOfBGHUtils, "ShouldBeHitByEMP");
        }

        public static bool Prefix(Thing target, ref bool __result)
        {
            Pawn targetPawn = target as Pawn;
            __result = targetPawn?.stances?.stunner?.StunFromEMP == false;

            return false;
        }
    }
}
