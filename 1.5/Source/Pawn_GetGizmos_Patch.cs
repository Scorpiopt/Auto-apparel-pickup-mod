using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AutoApparelPickup
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Pawn_GetGizmos_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
        {
            foreach (var gizmo in gizmos) 
                yield return gizmo;
            if (__instance.IsColonistPlayerControlled)
            {
                yield return new Command_Action
                {
                    defaultLabel = "AAP.EquipArmour".Translate(),
                    defaultDesc = "AAP.EquipArmourDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/EquipArmour"),
                    action = () =>
                    {
                        var armor = ArmorSearchUtility.PickBestArmorFor(__instance);
                        if (armor != null)
                        {
                            __instance.jobs.TryTakeOrderedJob(JobMaker.MakeJob(AAP_DefOf.AAP_EquipArmour, armor));
                        }
                        else
                        {
                            Messages.Message("AAP.NoArmourFoundToEquipFor".Translate(__instance.Named("PAWN")), MessageTypeDefOf.RejectInput);
                        }
                    }
                };
            }
        }
    }
}
