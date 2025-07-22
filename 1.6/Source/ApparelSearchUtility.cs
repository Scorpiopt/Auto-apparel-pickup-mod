using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace AutoApparelPickup
{
    [HotSwappable]
    [StaticConstructorOnStartup]
    public static class ApparelSearchUtility
    {
        public static HashSet<ThingDef> apparelDefs = new HashSet<ThingDef>();

        public static bool IsUsefulApparel(this ThingDef thingDef)
        {
            return apparelDefs.Contains(thingDef);
        }

        public static Func<Pawn, Thing, bool> baseApparelValidator = delegate (Pawn p, Thing x)
        {
            if (!apparelDefs.Contains(x.def))
            {
                return false;
            }
            if (!p.CanReserveAndReach(x, PathEndMode.OnCell, Danger.Deadly))
            {
                return false;
            }
            if (x is not Apparel apparel)
            {
                return false;
            }
            if (p.outfits?.CurrentApparelPolicy != null && p.outfits.CurrentApparelPolicy.filter.Allows(x) is false)
            {
                return false;
            }
            if (apparel.PawnCanWear(p) is false)
            {
                return false;
            }
            return true;
        };


        static ApparelSearchUtility()
        {
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (thingDef.IsApparel && (thingDef?.equippedStatOffsets?.Any(x => x?.value > 0) ?? false))
                {
                    apparelDefs.Add(thingDef);
                }
            }
        }

        public static ThingWithComps FindApparelFor(Pawn pawn, Job job, SkillDef skillDef, out ApparelAction apparelAction)
        {
            return FindApparelForInt(pawn, new SkillJob(skillDef, job), baseApparelValidator, out apparelAction);
        }

        private static ThingWithComps FindApparelForInt(Pawn pawn, SkillJob skillJob, Func<Pawn, Thing, bool> validator, 
            out ApparelAction apparelAction)
        {
            apparelAction = ApparelAction.DoNothing;
            var equippedThings = pawn.apparel.WornApparel.Where(x => validator(pawn, x));
            var inventoryThings = pawn.inventory?.innerContainer.OfType<ThingWithComps>().Where(x => validator(pawn, x));
            var outsideThings = new List<ThingWithComps>();
            foreach (var def in apparelDefs)
            {
                foreach (var apparel in pawn.Map.listerThings.ThingsOfDef(def).OfType<Apparel>())
                {
                    if (!pawn.apparel.WornApparel.Any(x => x.def == apparel.def) 
                        && !pawn.inventory.innerContainer.Any(x => x.def == apparel.def) 
                        && !pawn.apparel.WouldReplaceLockedApparel(apparel)
                        && validator(pawn, apparel))
                    {
                        outsideThings.Add(apparel as ThingWithComps);
                    }
                }
            }
            var equippedThingsScored = GetApparelsScoredFor(equippedThings, skillJob);
            var inventoryThingsScored = GetApparelsScoredFor(inventoryThings, skillJob);
            var outsideThingsScored = GetApparelsScoredFor(outsideThings, skillJob);
            return GetScoredApparel(pawn, equippedThingsScored, inventoryThingsScored, outsideThingsScored, out apparelAction);
        }

        private static ThingWithComps GetScoredApparel(Pawn pawn, Dictionary<float, List<ThingWithComps>> equippedThingsScored, Dictionary<float, List<ThingWithComps>> inventoryThingsScored,
            Dictionary<float, List<ThingWithComps>> outsideThingsScored, out ApparelAction apparelAction)
        {
            apparelAction = ApparelAction.DoNothing;
            while (true)
            {
                if ((!equippedThingsScored?.Any() ?? false) && (!inventoryThingsScored?.Any() ?? false) && (!outsideThingsScored?.Any() ?? false))
                {
                    break;
                }
                else
                {
                    var equippedMaxScore = (equippedThingsScored != null && equippedThingsScored.Any()) ? equippedThingsScored?.MaxBy(x => x.Key).Key : null;
                    var inventoryMaxScore = (inventoryThingsScored != null && inventoryThingsScored.Any()) ? inventoryThingsScored?.MaxBy(x => x.Key).Key : null;
                    var outsideMaxScore = (outsideThingsScored != null && outsideThingsScored.Any()) ? outsideThingsScored?.MaxBy(x => x.Key).Key : null;

                    if (equippedMaxScore.HasValue && (!inventoryMaxScore.HasValue || equippedMaxScore.Value >= inventoryMaxScore.Value)
                        && (!outsideMaxScore.HasValue || equippedMaxScore.Value >= outsideMaxScore))
                    {
                        return equippedThingsScored.RandomElement().Value.RandomElement();
                    }
                    else if (inventoryMaxScore.HasValue && (!equippedMaxScore.HasValue || inventoryMaxScore.Value > equippedMaxScore.Value)
                        && (!outsideMaxScore.HasValue || inventoryMaxScore.Value >= outsideMaxScore.Value))
                    {
                        apparelAction = ApparelAction.EquipFromInventory;
                        return inventoryThingsScored[inventoryMaxScore.Value].RandomElement();
                    }
                    else if (outsideMaxScore.HasValue && (!equippedMaxScore.HasValue || outsideMaxScore.Value > equippedMaxScore)
                        && (!inventoryMaxScore.HasValue || outsideMaxScore.Value > inventoryMaxScore.Value))
                    {
                        var apparels = outsideThingsScored[outsideMaxScore.Value];
                        var apparel = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, apparels, PathEndMode.OnCell, TraverseParms.For(pawn),
                            9999, (Thing x) => !x.IsForbidden(pawn)) as ThingWithComps;
                        if (apparel != null)
                        {
                            apparelAction = ApparelAction.GoAndEquipApparel;
                            return apparel;
                        }
                        else
                        {
                            outsideThingsScored.Remove(outsideMaxScore.Value);
                        }
                    }
                    else if (!equippedMaxScore.HasValue && !inventoryMaxScore.HasValue && !outsideMaxScore.HasValue)
                    {
                        return null;
                    }
                }
            }
            return null;
        }

        private static Dictionary<float, List<ThingWithComps>> GetApparelsScoredFor(IEnumerable<ThingWithComps> things, SkillJob skillJob)
        {
            if (things.Any())
            {
                return GetScoredThings(things, skillJob);
            }
            return null;
        }
        private static Dictionary<float, List<ThingWithComps>> GetScoredThings(IEnumerable<ThingWithComps> things, SkillJob skillJob)
        {
            Dictionary<float, List<ThingWithComps>> apparelsByScores = new Dictionary<float, List<ThingWithComps>>();
            foreach (var thing in things)
            {
                if (thing.TryGetScore(skillJob, out var score))
                {
                    if (apparelsByScores.TryGetValue(score, out var apparelList))
                    {
                        apparelList.Add(thing);
                    }
                    else
                    {
                        apparelsByScores[score] = new List<ThingWithComps> { thing };
                    }
                }
            }
            return apparelsByScores;
        }

        public static bool TryGetScore(this ThingWithComps apparel, SkillJob skillJob, out float result)
        {
            bool isUseful = false;
            result = 0;
            if (skillJob.skill != null)
            {
                if (apparel.def.equippedStatOffsets != null)
                {
                    foreach (var stat in apparel.def.equippedStatOffsets)
                    {
                        if (stat.AffectsSkill(skillJob.skill))
                        {
                            if (stat.value > 0)
                            {
                                isUseful = true;
                            }
                            result += stat.value;
                        }
                    }
                }
            }
            if (skillJob.job != null) // maybe we should add scores for apparels here
            {
                if (skillJob.job.bill?.recipe?.workSpeedStat != null)
                {
                    if (apparel.def.equippedStatOffsets != null)
                    {
                        foreach (var stat in apparel.def.equippedStatOffsets)
                        {
                            if (stat.stat == skillJob.job.bill.recipe.workSpeedStat)
                            {
                                if (stat.value > 0)
                                {
                                    isUseful = true;
                                }
                                result += stat.value;
                            }
                        }
                    }
                }
            }
            return isUseful;
        }

        public static bool AffectsSkill(this StatModifier statModifier, SkillDef skill)
        {
            if (statModifier.stat.skillNeedOffsets != null)
            {
                foreach (var skillNeed in statModifier.stat.skillNeedOffsets)
                {
                    if (skill == skillNeed.skill)
                    {
                        return true;
                    }
                }
            }

            if (statModifier.stat.skillNeedFactors != null)
            {
                foreach (var skillNeed in statModifier.stat.skillNeedFactors)
                {
                    if (skill == skillNeed.skill)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static SkillDef GetActiveSkill(this Job job, Pawn pawn)
        {
            return GetActiveSkill(job, pawn.jobs.curDriver.toils);
        }

        public static SkillDef GetActiveSkill(Job job, List<Toil> toils)
        {
            foreach (var toil in toils)
            {
                if (toil?.activeSkill != null)
                {
                    try
                    {
                        var skill = toil.activeSkill();
                        if (skill != null)
                        {
                            return skill;
                        }
                    }
                    catch (Exception ex)
                    {

                    };
                }
            }

            if (job != null)
            {
                if (job.def == JobDefOf.FinishFrame)
                {
                    return SkillDefOf.Construction;
                }
            }
            if (job.workGiverDef != null)
            {
                return job.workGiverDef?.workType?.relevantSkills?.FirstOrDefault();
            }
            return null;
        }
    }
}
