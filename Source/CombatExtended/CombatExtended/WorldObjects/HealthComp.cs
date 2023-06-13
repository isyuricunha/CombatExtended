﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace CombatExtended.WorldObjects
{
    public class HealthComp : WorldObjectComp, IWorldCompCE
    {
        public const float HEALTH_HEALRATE_DAY = 0.15f;

        private int lastTick = -1;
        private int _configTick = -1;
        private float health = 1.0f;
        private float negateChance = 0f;
        private float armorDamageMultiplier = 1f;
        private bool destroyedInstantly = false;
        public float Health
        {
            get => health;
            set => health = Mathf.Clamp01(value);
        }
        public float TotalDamageRequired
        {
            get => DestoyedInstantly ? 1f : 100 * ArmorDamageMultiplier;
        }
        public float DamageRequired
        {
            get => TotalDamageRequired * health;
        }
        public float HealingRatePerTick
        {
            get => parent.Faction != null ? ((int)parent.Faction.def.techLevel) / 4f * HEALTH_HEALRATE_DAY / 60000f : 0f;
        }

        public float ArmorDamageMultiplier
        {
            get
            {
                UpdateCacheValues();
                return armorDamageMultiplier;
            }
            private set => armorDamageMultiplier = value;
        }
        public float NegateChance
        {
            get
            {
                UpdateCacheValues();
                return negateChance;
            }
            private set => negateChance = Mathf.Clamp01(value);
        }
        public bool DestoyedInstantly
        {
            get
            {
                UpdateCacheValues();
                return destroyedInstantly;
            }
            private set => destroyedInstantly = value;
        }

        public WorldObjectCompProperties_Health Props
        {
            get => props as WorldObjectCompProperties_Health;
        }
        public void UpdateCacheValues()
        {
            if (_configTick == GenTicks.TicksGame)
            {
                return;
            }
            _configTick = GenTicks.TicksGame;

            bool techLevelNoImpact = Props.techLevelNoImpact;
            ArmorDamageMultiplier = 1f;
            NegateChance = 0f;
            DestoyedInstantly = Props?.destoyedInstantly ?? false;

            if (Props.healthModifier > 0f)
            {
                ArmorDamageMultiplier *= Props.healthModifier;
            }

            var factionExtension = parent.Faction?.def.GetModExtension<WorldObjectHealthExtension>();

            if (factionExtension != null)
            {
                if (factionExtension.healthModifier > 0f)
                {
                    ArmorDamageMultiplier *= factionExtension.healthModifier;
                }
                if (factionExtension.chanceToNegateDamage >= 0f)
                {
                    NegateChance = factionExtension.chanceToNegateDamage;
                }
                techLevelNoImpact |= factionExtension.techLevelNoImpact;
                DestoyedInstantly |= factionExtension.destoyedInstantly;

            }


            if (parent is Site site)
            {
                foreach (var sitePart in site.parts)
                {
                    var sitePartExtension = sitePart?.def?.GetModExtension<WorldObjectHealthExtension>();
                    if (sitePartExtension != null)
                    {
                        if (sitePartExtension.healthModifier > 0f)
                        {
                            ArmorDamageMultiplier *= sitePartExtension.healthModifier;
                        }
                        if (sitePartExtension.chanceToNegateDamage >= 0f)
                        {
                            NegateChance = sitePartExtension.chanceToNegateDamage;
                        }
                        techLevelNoImpact |= sitePartExtension.techLevelNoImpact;
                        DestoyedInstantly |= sitePartExtension.destoyedInstantly;
                    }

                }
            }

            if (!techLevelNoImpact)
            {
                ArmorDamageMultiplier *= (parent.Faction != null ? Mathf.Max((float)parent.Faction.def.techLevel, 1f) : 1f);
            }
        }
        public HealthComp()
        {
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref health, "health", 1.0f);
            Scribe_Values.Look(ref lastTick, "lastTick", -1);
        }

        public void ThrottledCompTick()
        {
            Health += HealingRatePerTick * WorldObjectTrackerCE.THROTTLED_TICK_INTERVAL;
        }
        void TryFinishDestroyQuests(Map launcherMap)
        {
            QuestUtility.SendQuestTargetSignals(parent.questTags, "AllEnemiesDefeated", parent.Named("SUBJECT"), new NamedArgument(launcherMap, "MAP"));
            foreach (var quest in RelatedQuests)
            {
                quest.End(QuestEndOutcome.Fail);
            }
            IdeoUtility.Notify_PlayerRaidedSomeone(launcherMap.mapPawns.FreeColonistsSpawned);
        }

        IEnumerable<Quest> RelatedQuests => Find.QuestManager.QuestsListForReading.Where(x => !x.Historical && x.QuestLookTargets.Contains(parent));
        public void ApplyDamage(float amount, Faction attackingFaction, Map launcherMap)
        {
            if (Rand.Chance(NegateChance))
            {
                Log.Message($"Shell negated with chance {negateChance}");
                return;
            }
            if (DestoyedInstantly)
            {
                TryFinishDestroyQuests(launcherMap);
                TryDestroy();
                return;
            }
            Health -= amount / ArmorDamageMultiplier;
            Notify_DamageTaken(attackingFaction, launcherMap);
        }
        void TryDestroy()
        {
            if (!parent.Destroyed)
            {
                parent.Destroy();
            }
        }
        public void Notify_DamageTaken(Faction attackingFaction, Map launcherMap)
        {
            if (health <= 1e-4)
            {
                TryFinishDestroyQuests(launcherMap);
                Destroy();
                return;
            }
        }

        public void Destroy(Faction attackingFaction = null)
        {
            int tile = parent.Tile;
            Faction faction = parent.Faction;
            FactionStrengthTracker tracker = faction.GetStrengthTracker();
            if (parent is Settlement settlement)
            {
                string message;
                if (faction == null)
                {
                    message = "CE_Message_SettlementDestroyed_Description".Translate().Formatted(parent.Label);
                }
                else
                {
                    if (attackingFaction != null)
                    {
                        message = "CE_Message_SettlementDestroyed_Description_Responsibility".Translate(attackingFaction.Name, parent.Label, faction.Name);
                    }
                    else
                    {
                        message = "CE_Message_SettlementDestroyed_Faction_Description".Translate().Formatted(parent.Label, faction.Name);
                    }
                }
                Find.LetterStack.ReceiveLetter("CE_Message_SettlementDestroyed_Label".Translate(), message, LetterDefOf.NeutralEvent);
                TryDestroy();
                DestroyedSettlement destroyedSettlement = (DestroyedSettlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.DestroyedSettlement);
                destroyedSettlement.tileInt = tile;
                if (faction != null)
                {
                    destroyedSettlement.SetFaction(faction);
                }
                destroyedSettlement.SpawnSetup();
                Find.World.worldObjects.Add(destroyedSettlement);
                if (tracker != null)
                {
                    tracker.Notify_SettlementDestroyed();
                }
            }
            else
            {
                string message = null;
                if (faction == null)
                {
                    message = "CE_Message_SiteDestroyed".Translate().Formatted(parent.Label);
                }
                else
                {
                    message = "CE_Message_SiteDestroyed_Faction".Translate().Formatted(parent.Label, faction.Name);
                }
                Messages.Message(message, MessageTypeDefOf.SituationResolved);
                if (tracker != null && parent is Site)
                {
                    tracker.Notify_SiteDestroyed();
                }
                TryDestroy();
            }
            if (faction != null && faction.def.humanlikeFaction && attackingFaction != null && attackingFaction != faction) // check the projectile faction
            {
                FactionRelation relation = faction.RelationWith(attackingFaction, true);
                if (relation == null)
                {
                    faction.TryMakeInitialRelationsWith(attackingFaction);
                    relation = faction.RelationWith(attackingFaction, true);
                }
                faction.TryAffectGoodwillWith(attackingFaction, -100, true, true, HistoryEventDefOf.DestroyedEnemyBase, null);
            }
        }
        public override string CompInspectStringExtra()
        {
            if (DamageRequired != TotalDamageRequired || Prefs.DevMode)
            {
                return $"{DamageRequired:0}/{TotalDamageRequired:0} HP";
            }
            return base.CompInspectStringExtra();
        }
    }
}
