using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AutoApparelPickup
{
    public class JobDriver_WearArmor : JobDriver_Wear
    {
        public override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var toil in base.MakeNewToils())
            {
                yield return toil;
            }
            yield return new Toil
            {
                initAction = () =>
                {
                    var armor = ArmorSearchUtility.PickBestArmorFor(pawn);
                    if (armor != null)
                    {
                        pawn.jobs.StartJob(JobMaker.MakeJob(AAP_DefOf.AAP_EquipArmour, armor), JobCondition.Succeeded);
                    }
                }
            };
        }
    }
}
