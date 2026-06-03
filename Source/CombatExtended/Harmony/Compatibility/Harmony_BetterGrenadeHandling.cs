using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace CombatExtended.HarmonyCE.Compatibility;

public class Harmony_BetterGrenadeHandling  //manually patched from HarmonyBase
{
    internal static Type TypeOfBGHUtils
    {
        get
        {
            return AccessTools.TypeByName("BetterGrenadeHandling.BGHUtils");
        }
    }

    public static bool Prefix(Thing target, ref bool __result)
    {
        Pawn targetPawn = target as Pawn;
        __result = targetPawn?.stances?.stunner?.StunFromEMP == false;

        return false;
    }
}
