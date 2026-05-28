using Verse;
using RimWorld;
using System.Collections.Generic;
using HarmonyLib;
using VEF.Apparels;
using VEF.Weapons;

namespace CombatExtended.Compatibility.VEFCompat;

[HarmonyPatch(typeof(CompShieldBubble), nameof(CompShieldBubble.AbsorbingDamage))]
public static class Harmony_CompShieldBubble_Patch
{

    // Replace VEF comp with a copy of our patched version.
    // Modified with their additional checks and tesla projectile deflect
    public static bool Prefix(out bool absorbed, DamageInfo dinfo, CompShieldBubble __instance)
    {
        absorbed = false;
        TeslaProjectile.wasDeflected = absorbed;
        if (__instance.ShieldState != ShieldState.Active || __instance.Pawn == null)
        {
            return false;
        }
        if (dinfo.Def.ignoreShields)
        {
            return false;
        }
        if (((!__instance.Props.blockRangedAttack || (!dinfo.Def.isRanged && !dinfo.Def.isExplosive && dinfo.Def != DamageDefOf.EMP)) && (!__instance.Props.blockMeleeAttack || ((dinfo.Weapon != null || dinfo.Instigator is not Pawn) && (!(dinfo.Weapon?.IsMeleeWeapon ?? false))))))
        {
            return false;
        }
        absorbed = true;
        TeslaProjectile.wasDeflected = absorbed;
        float shieldDamageMultiplier = 1f;
        float secondaryShieldDamageAmount = 0f;
        if (dinfo.Weapon?.projectile is ProjectilePropertiesCE projectilePropertiesCe)
        {
            shieldDamageMultiplier = projectilePropertiesCe.shieldDamageMultiplier;
            List<SecondaryDamage> secondaryDamageProperties = projectilePropertiesCe.secondaryDamage;
            if (!secondaryDamageProperties.NullOrEmpty())
            {
                foreach (SecondaryDamage secondaryDamageInfo in secondaryDamageProperties)
                {
                    var secondaryDamageModExt = secondaryDamageInfo.def.GetModExtension<DamageDefExtensionCE>();
                    if ((secondaryDamageInfo.def.harmsHealth || (secondaryDamageModExt?.secondaryDamageShieldOverride ?? false)) && Rand.Chance(secondaryDamageInfo.chance))
                    {
                        var secondaryDamageMultiplierValue = secondaryDamageInfo.shieldDamageMultiplier;
                        if (secondaryDamageMultiplierValue == 1f && secondaryDamageModExt != null && secondaryDamageModExt.shieldDamageMultiplier != secondaryDamageMultiplierValue)
                        {
                            secondaryDamageMultiplierValue = secondaryDamageModExt.shieldDamageMultiplier;
                        }
                        secondaryShieldDamageAmount += (secondaryDamageInfo.amount * secondaryDamageMultiplierValue);
                        dinfo.amountInt += secondaryDamageInfo.amount;

                    }
                }
            }
        }
        if (shieldDamageMultiplier == 1f)
        {
            DamageDefExtensionCE primaryDamageModExt = dinfo.defInt.GetModExtension<DamageDefExtensionCE>();
            if (primaryDamageModExt != null && primaryDamageModExt.shieldDamageMultiplier != shieldDamageMultiplier)
            {
                shieldDamageMultiplier = primaryDamageModExt.shieldDamageMultiplier;
            }
        }
        float primaryDamage = dinfo.Amount * shieldDamageMultiplier;
        float totalDamage = (primaryDamage + secondaryShieldDamageAmount) * __instance.EnergyLossPerDamage;
#if DEBUG
        if (Controller.settings.DebugVerbose)
        {
            Log.Message($"Primary Damage: {primaryDamage} Secondary Damage: {secondaryShieldDamageAmount}  Shield Energy Loss Per Damage: {__instance.EnergyLossPerDamage} Shield Damage Before Energy Multiplier: {primaryDamage + secondaryShieldDamageAmount} Actual Shield Energy Damage: {totalDamage * 100} ");
        }
#endif
        __instance.energy -= totalDamage;

        if (__instance.energy < 0f)
        {
            __instance.Break();
        }
        else
        {
            dinfo.amountInt -= secondaryShieldDamageAmount;
            __instance.AbsorbedDamage(dinfo);
        }
        return false;
    }
}

