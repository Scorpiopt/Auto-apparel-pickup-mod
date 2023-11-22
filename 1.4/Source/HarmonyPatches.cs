using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace AutoApparelPickup
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("AutoApparelPickupMod");
            var methodPostfix = AccessTools.Method(typeof(HarmonyPatches), nameof(AddEquipApparelToilsPostfix));
            foreach (var type in typeof(JobDriver).AllSubclasses())
            {
                try
                {
                    var makeNewToilsMethod = AccessTools.DeclaredMethod(type, "MakeNewToils");
                    if (makeNewToilsMethod != null)
                    {
                        harmony.Patch(makeNewToilsMethod, null, new HarmonyMethod(methodPostfix, priority: Priority.Last));
                    }
                }
                catch { }
            }
            harmony.PatchAll();
        }
        private static HashSet<JobDef> ignoredJobs = new HashSet<JobDef>
        {
            JobDefOf.GotoWander,
            JobDefOf.Ingest,
            JobDefOf.LayDown,
            JobDefOf.Wait_MaintainPosture,
            JobDefOf.Wait,
            JobDefOf.HaulToCell,
            JobDefOf.TakeInventory,
            JobDefOf.Wait_Downed,
            JobDefOf.Wait_Wander,
            JobDefOf.FleeAndCower,
            JobDefOf.Goto,
            JobDefOf.Wait_Combat,
        };
        private static Dictionary<Job, Thing> cachedApparelsByJobs = new Dictionary<Job, Thing>();
        public static void AddEquipApparelToilsPostfix(ref IEnumerable<Toil> __result, JobDriver __instance)
        {
            try
            {
                var pawn = __instance.pawn;
                if (pawn != null && pawn.RaceProps.Humanlike && __instance.job != null && !ignoredJobs.Contains(__instance.job.def))
                {
                    var list = __result.ToList();
                    var skill = ApparelSearchUtility.GetActiveSkill(pawn.CurJob, list);
                    if (pawn.Map != null)
                    {
                        ApparelAction apparelAction;
                        var apparel = ApparelSearchUtility.FindApparelFor(pawn, __instance.job, skill, out apparelAction);
                        cachedApparelsByJobs[pawn.CurJob] = apparel; // we do that so we retrieve this apparel later rather than finding it again
                        if (apparel != null)
                        {
                            Toil equipApparel = new Toil();
                            equipApparel.initAction = delegate
                            {
                                EquipApparel(pawn, apparel);
                            };
                            if (apparelAction == ApparelAction.GoAndEquipApparel)
                            {
                                list.Insert(0, equipApparel);
                            }
                            else if (apparelAction == ApparelAction.EquipFromInventory)
                            {
                                pawn.inventory.innerContainer.TryDrop(apparel, ThingPlaceMode.Direct, out var lastResultingThing);
                                cachedApparelsByJobs[pawn.CurJob] = lastResultingThing;
                                list.Insert(0, equipApparel);
                            }
                        }
                    }
                    __result = list;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception found: " + ex);
            }
        }

        public static void EquipApparel(Pawn pawn, ThingWithComps apparel)
        {
            pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wear, apparel), JobCondition.InterruptForced, resumeCurJobAfterwards: true);
        }
    }
}
