using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CombatExtended.HarmonyCE;
using HarmonyLib;
using RimWorld;
using VEF.Apparels;
using Verse;

namespace CombatExtended.Compatibility.VEFCompat
{

    [HarmonyPatch(typeof(CompShieldField), nameof(CompShieldField.AbsorbDamage), typeof(float), typeof(DamageDef), typeof(float))]
    static class CompShieldField_AbsorbDamage
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var getProps = AccessTools.Method(typeof(CompShieldField), "get_Props");
            var disarmedByEmpForTicks = AccessTools.Field(typeof(CompProperties_ShieldField), nameof(CompProperties_ShieldField.disarmedByEmpForTicks));
            var breakShield = AccessTools.Method(typeof(CompShieldField), nameof(CompShieldField.BreakShield));
            var replacement = AccessTools.Method(typeof(CompShieldField_AbsorbDamage), nameof(OnEmpAbsorbed));

            int startIndex = codes.FindIndex(c => c.Calls(getProps) && codes[codes.IndexOf(c) + 1].LoadsField(disarmedByEmpForTicks));
            int endIndex = codes.FindIndex(startIndex, c => c.Calls(breakShield));

            int indexesToRemove = endIndex - startIndex + 1;

            if (indexesToRemove > 0)
            {

                codes.RemoveRange(startIndex, indexesToRemove);
                codes.InsertRange(startIndex, [
                    new CodeInstruction(OpCodes.Ldarg_1), // damageAmount
                    new CodeInstruction(OpCodes.Ldarg_2), // damageDef
                    new CodeInstruction(OpCodes.Ldarg_3), // angle
                    new CodeInstruction(OpCodes.Call, replacement)
                ]);
            }
            else
            {
                Log.Error($"Combat Extended :: Failed to find first injection point when applying Patch: {HarmonyBase.GetClassName(MethodBase.GetCurrentMethod()?.DeclaringType)}");
            }
            return codes;
        }

        public static void OnEmpAbsorbed(CompShieldField shield, float amount, DamageDef def, float angle)
        {
            if (shield.Indestructible && shield.Props.disarmedByEmpForTicks == -1 && def == DamageDefOf.EMP)
            {
                shield.BreakShield(new DamageInfo(def, amount, 0, angle));
            }
        }

    }
}
