﻿using ProjectRimFactory.AutoMachineTool;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;
using Verse;
using Verse.Sound;

namespace CombatExtended
{
    public abstract class VerbCIWS : Verb_ShootCE_CIWS, ITargetSearcher, IVerbDisableable
    {
        protected bool debug = true;
        protected Texture2D icon;
        public virtual bool HoldFire { get; set; }

        public VerbProperties_CIWS Props => verbProps as VerbProperties_CIWS;
        public virtual string HoldFireLabel => Props.holdFireLabel;
        public virtual string HoldFireDesc => Props.holdFireDesc;
        public virtual Texture2D HoldFireIcon
        {
            get
            {
                if (icon == null)
                {
                    icon = ContentFinder<Texture2D>.Get(Props.holdFireIcon);
                }
                return icon;
            }
        }
        protected override bool ShouldAim => false;
        public virtual bool Active => !HoldFire && Turret.Active;
        protected override bool LockRotationAndAngle => false;
        public abstract bool TryFindNewTarget(out LocalTargetInfo target);
        public virtual void ShowTrajectories()
        {
            if (lastShootLine != null)
            {
                Caster.Map.debugDrawer.FlashLine(lastShootLine.Value.source, lastShootLine.Value.Dest, 60, SimpleColor.Green);
            }
        }
        protected (Vector2 firstPos, Vector2 secondPos) PositionOfCIWSProjectile(int sinceTicks, Vector3 targetPos, bool drawPos = false)
        {
            var firstPos = Caster.Position.ToVector3Shifted();
            var secondPos = firstPos;
            var originV3 = firstPos;
            var originV2 = new Vector2(originV3.x, originV3.z);
            var shotAngle = ShotAngle(targetPos);
            var shotRotation = ShotRotation(targetPos);

            var destination = projectilePropsCE.TrajectoryWorker.Destination(originV2, shotRotation, ShotHeight, ShotSpeed, shotAngle, projectilePropsCE.Gravity);
            var flightTime = projectilePropsCE.TrajectoryWorker.GetFlightTime(shotAngle, ShotSpeed, projectilePropsCE.Gravity, ShotHeight) * GenTicks.TicksPerRealSecond;
            var initialVelocity = projectilePropsCE.TrajectoryWorker.GetVelocity(ShotSpeed, shotRotation, shotAngle);
            var enumeration = projectilePropsCE.TrajectoryWorker.NextPositions(currentTarget, shotRotation, shotAngle, projectilePropsCE.Gravity, originV2, this.Caster.Position.ToVector3Shifted(), destination, (int)flightTime, flightTime, ShotHeight, false, initialVelocity, ShotSpeed, originV3, projectilePropsCE.mass.max, projectilePropsCE.ballisticCoefficient.max, projectilePropsCE.diameter.max / 2000, projectilePropsCE.Gravity, ShotSpeed, projectilePropsCE.speedGain, projectilePropsCE.speed, 0).GetEnumerator();
            for (int i = 1; i <= sinceTicks; i++)
            {
                firstPos = secondPos;

                if (!enumeration.MoveNext())
                {
                    break;
                }
                secondPos = enumeration.Current;

            }
            if (drawPos)
            {
                firstPos = projectilePropsCE.TrajectoryWorker.ExactPosToDrawPos(firstPos, sinceTicks - 1, projectilePropsCE.TickToTruePos, Projectile.Altitude);
                secondPos = projectilePropsCE.TrajectoryWorker.ExactPosToDrawPos(secondPos, sinceTicks, projectilePropsCE.TickToTruePos, Projectile.Altitude);
            }
            return (new Vector2(firstPos.x, firstPos.z), new Vector2(secondPos.x, secondPos.z));
        }

        public override bool TryCastShot()
        {
            var result = base.TryCastShot();
            if (result && debug)
            {
                ShowTrajectories();
            }
            return result;
        }
        protected override bool KeepBurstOnNoShootLine(bool suppressing, out ShootLine shootLine)
        {
            shootLine = lastShootLine.HasValue ? lastShootLine.Value : default;
            return !currentTarget.ThingDestroyed;
        }
        public override bool Available()
        {
            return Active && base.Available();
        }
    }
    public abstract class VerbCIWS<TargetType> : VerbCIWS where TargetType : Thing
    {

        public override bool TryFindNewTarget(out LocalTargetInfo target)
        {
            if (!Active)
            {
                target = LocalTargetInfo.Invalid;
                return false;
            }
            float range = this.verbProps.range;
            var _target = Targets.Where(x => Props.Interceptable(x.def) && !Props.Ignored.Contains(x.def) && !Turret.IgnoredDefsSettings.Contains(x.def)).Where(x => !IsFriendlyTo(x)).FirstOrDefault(t =>
            {
                var verb = this;
                if (Caster.Map.GetComponent<TurretTracker>().CIWS.Any(turret => turret.currentTargetInt.Thing == t) || ProjectileCE_CIWS.ProjectilesAt(Caster.Map).Any(x => x.intendedTarget.Thing == t))
                {
                    return false;
                }
                float num = verb.verbProps.EffectiveMinRange(t, this.Caster);
                if (!verb.TryFindCEShootLineFromTo(Caster.Position, t, out var shootLine, out var targetPos))
                {
                    return false;
                }
                var intersectionPoint = shootLine.Dest;
                float num2 = intersectionPoint.DistanceToSquared(Caster.Position);
                return num2 > num * num && num2 < range * range;
            });
            if (_target != null)
            {
                target = _target;
                return true;
            }
            target = null;
            return false;
        }
        protected virtual bool IsFriendlyTo(TargetType thing) => !thing.HostileTo(Caster);
        public abstract IEnumerable<TargetType> Targets { get; }
        //public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true) => target.Thing is TargetType && TryFindCEShootLineFromTo(Caster.Position, target, out _, out _) && base.ValidateTarget(target, showMessages);
        protected abstract IEnumerable<Vector3> TargetNextPositions(TargetType target);
        public override bool TryFindCEShootLineFromTo(IntVec3 root, LocalTargetInfo targetInfo, out ShootLine resultingLine, out Vector3 targetPos)
        {

            if (!(targetInfo.Thing is TargetType target))
            {
                resultingLine = default;
                targetPos = default;
                return false;
            }
            var maxDistSqr = Props.range * Props.range;
            var originV3 = Caster.Position.ToVector3Shifted();
            if (!Props.tryPredict)
            {
                if ((originV3 - target.DrawPos).MagnitudeHorizontalSquared() > maxDistSqr)
                {
                    resultingLine = default;
                    targetPos = default;
                    return false;
                }
                var y = TargetNextPositions(target).FirstOrDefault().y;
                targetPos = target.DrawPos;
                resultingLine = new ShootLine(Shooter.Position, new IntVec3((int)targetPos.x, (int)y, (int)targetPos.z));
                return true;
            }
            var midBurst = numShotsFired > 0;
            var ticksToSkip = (Caster as Building_TurretGunCE)?.CurrentTarget.IsValid ?? CurrentTarget.IsValid ? this.BurstWarmupTicksLeft : VerbPropsCE.warmupTime.SecondsToTicks();
            var instant = projectilePropsCE.isInstant;
            if (instant)
            {
                var to = TargetNextPositions(target).Skip(ticksToSkip).FirstOrFallback(Vector3.negativeInfinity);
                if (to == Vector3.negativeInfinity)
                {
                    resultingLine = default;
                    targetPos = default;
                    return false;
                }
                targetPos = to;
                resultingLine = new ShootLine(originV3.ToIntVec3(), to.ToIntVec3());
                return true;
            }
            int i = 1;

            var targetPos1 = new Vector2(target.DrawPos.x, target.DrawPos.z);
            foreach (var pos in TargetNextPositions(target).Skip(ticksToSkip))
            {
                var targetPos2 = new Vector2(pos.x, pos.z);
                if ((pos - originV3).MagnitudeHorizontalSquared() > maxDistSqr)
                {
                    targetPos1 = targetPos2;
                    i++;
                    continue;
                }

                Vector2 originV2 = new Vector2(originV3.x, originV3.z);

                var positions = PositionOfCIWSProjectile(i, pos, true);
                //if (positions.firstPos == positions.secondPos) //Not sure why, but sometimes this code drops calculations on i = 1
                //{
                //    resultingLine = default(ShootLine);
                //    return false;
                //}
                Vector2 ciwsPos1 = positions.firstPos, ciwsPos2 = positions.secondPos;

                if (CE_Utility.TryFindIntersectionPoint(ciwsPos1, ciwsPos2, targetPos1, targetPos2, out _))
                {
                    targetPos = pos;

                    resultingLine = new ShootLine(Shooter.Position, new IntVec3((int)pos.x, (int)pos.y, (int)pos.y));

                    return true;
                }
                targetPos1 = targetPos2;
                i++;
                if (i > maximumPredectionTicks)
                {
                    break;
                }
            }
            resultingLine = default;
            targetPos = default;
            return false;
        }
        public override float GetTargetHeight(LocalTargetInfo target, Thing cover, bool roofed, Vector3 targetLoc)
        {
            if (target.Thing is TargetType targ)
            {
                return targetLoc.y;
            }
            return base.GetTargetHeight(target, cover, roofed, targetLoc);
        }
    }
    public abstract class VerbCIWS_Comp<TargetType> : VerbCIWS<Thing> where TargetType : CompCIWSTarget
    {
        public override IEnumerable<Thing> Targets => CompCIWSTarget.Targets<TargetType>(Caster.Map);
        protected override bool IsFriendlyTo(Thing thing) => thing.TryGetComp<TargetType>()?.IsFriendlyTo(thing) ?? base.IsFriendlyTo(thing);
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true) => target.HasThing && target.Thing.HasComp<TargetType>() && base.ValidateTarget(target, showMessages);
        protected override IEnumerable<Vector3> TargetNextPositions(Thing target)
        {
            return target.TryGetComp<CompCIWSTarget>().NextPositions;
        }
    }

    public abstract class VerbProperties_CIWS : VerbPropertiesCE
    {
        public bool tryPredict = true;
        public string holdFireIcon = "UI/Commands/HoldFire";
        public string holdFireLabel = "HoldFire";
        public string holdFireDesc;
        public List<ThingDef> ignored = new List<ThingDef>();
        public IEnumerable<ThingDef> Ignored => ignored;
        public virtual bool Interceptable(ThingDef targetDef) => true;

        private IEnumerable<ThingDef> allTargets;

        public IEnumerable<ThingDef> AllTargets => allTargets ??= InitAllTargets();
        protected abstract IEnumerable<ThingDef> InitAllTargets();
    }
}
