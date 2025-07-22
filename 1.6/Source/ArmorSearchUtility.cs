using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AutoApparelPickup
{
    [HotSwappable]
    public static class ArmorSearchUtility
    {
        private static List<float> wornApparelScores = new List<float>();

        private static readonly SimpleCurve HitPointsPercentScoreFactorCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(0.2f, 0.2f),
            new CurvePoint(0.22f, 0.6f),
            new CurvePoint(0.5f, 0.6f),
            new CurvePoint(0.52f, 1f)
        };

        public static Thing PickBestArmorFor(Pawn pawn)
        {
            var CurrentApparelPolicy = pawn.outfits.CurrentApparelPolicy;
            Thing thing = null;
            float num2 = 0f;
            List<Thing> list = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel);
            if (list.Count == 0)
            {
                return null;
            }
            wornApparelScores = new List<float>();
            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                wornApparelScores.Add(ApparelScoreRaw(pawn, wornApparel[i]));
            }
            for (int j = 0; j < list.Count; j++)
            {
                Apparel apparel = (Apparel)list[j];
                if (!apparel.IsBurning() && CurrentApparelPolicy.filter.Allows(apparel) && !apparel.IsForbidden(pawn)
                    && (apparel.def.apparel.gender == Gender.None || apparel.def.apparel.gender == pawn.gender))
                {
                    float num3 = ApparelScoreGain_NewTmp(pawn, apparel, wornApparelScores);
                    if (!(num3 < 0.05f) && !(num3 < num2) && (!CompBiocodable.IsBiocoded(apparel) || CompBiocodable.IsBiocodedFor(apparel, pawn))
                        && ApparelUtility.HasPartsToWear(pawn, apparel.def)
                        && pawn.CanReserveAndReach(apparel, PathEndMode.OnCell, pawn.NormalMaxDanger()))
                    {
                        thing = apparel;
                        num2 = num3;
                    }
                }
            }
            return thing;
        }
        public static float ApparelScoreGain_NewTmp(Pawn pawn, Apparel ap, List<float> wornScoresCache)
        {
            if (ap.def == ThingDefOf.Apparel_ShieldBelt && pawn.equipment.Primary != null && pawn.equipment.Primary.def.IsWeaponUsingProjectiles)
            {
                return -1000f;
            }
            float num = ApparelScoreRaw(pawn, ap);
            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            bool flag = false;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                if (!ApparelUtility.CanWearTogether(wornApparel[i].def, ap.def, pawn.RaceProps.body))
                {
                    if (!pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(wornApparel[i]) || pawn.apparel.IsLocked(wornApparel[i]))
                    {
                        return -1000f;
                    }
                    num -= wornScoresCache[i];
                    flag = true;
                }
            }
            if (!flag)
            {
                num *= 10f;
            }
            return num;
        }

        public static float ApparelScoreGain(Pawn pawn, Apparel ap)
        {
            wornApparelScores.Clear();
            for (int i = 0; i < pawn.apparel.WornApparel.Count; i++)
            {
                wornApparelScores.Add(ApparelScoreRaw(pawn, pawn.apparel.WornApparel[i]));
            }
            return ApparelScoreGain_NewTmp(pawn, ap, wornApparelScores);
        }

        public static float OverallArmorValue(Pawn pawn)
        {
            return (GetArmorValue(pawn, StatDefOf.ArmorRating_Blunt) * 100f) + (GetArmorValue(pawn, StatDefOf.ArmorRating_Sharp) * 100f);
        }

        private static float GetArmorValue(Pawn pawn, StatDef stat)
        {
            float num = 0f;
            float num2 = Mathf.Clamp01(pawn.GetStatValue(stat) / 2f);
            List<BodyPartRecord> allParts = pawn.RaceProps.body.AllParts;
            List<Apparel> list = pawn.apparel?.WornApparel;
            for (int i = 0; i < allParts.Count; i++)
            {
                float num3 = 1f - num2;
                if (list != null)
                {
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (list[j].def.apparel.CoversBodyPart(allParts[i]))
                        {
                            float num4 = Mathf.Clamp01(list[j].GetStatValue(stat) / 2f);
                            num3 *= 1f - num4;
                        }
                    }
                }
                num += allParts[i].coverageAbs * (1f - num3);
            }
            return Mathf.Clamp(num * 2f, 0f, 2f);
        }

        public static float ApparelScoreRaw(Pawn pawn, Apparel ap)
        {
            float num = 0.1f + ap.def.apparel.scoreOffset;
            float num2 = ap.GetStatValue(StatDefOf.ArmorRating_Sharp)
                + ap.GetStatValue(StatDefOf.ArmorRating_Blunt);
            num += num2;
            if (ap.def.useHitPoints)
            {
                float x = ap.HitPoints / (float)ap.MaxHitPoints;
                num *= HitPointsPercentScoreFactorCurve.Evaluate(x);
            }
            num += ap.GetSpecialApparelScoreOffset();
            if (ap.WornByCorpse && (pawn == null || ThoughtUtility.CanGetThought(pawn, ThoughtDefOf.DeadMansApparel, checkIfNullified: true)))
            {
                num -= 0.5f;
                if (num > 0f)
                {
                    num *= 0.1f;
                }
            }
            if (ap.Stuff == ThingDefOf.Human.race.leatherDef)
            {
                if (pawn == null || ThoughtUtility.CanGetThought(pawn, ThoughtDefOf.HumanLeatherApparelSad, checkIfNullified: true))
                {
                    num -= 0.5f;
                    if (num > 0f)
                    {
                        num *= 0.1f;
                    }
                }
                if (pawn != null && ThoughtUtility.CanGetThought(pawn, ThoughtDefOf.HumanLeatherApparelHappy, checkIfNullified: true))
                {
                    num += 0.12f;
                }
            }
            if (pawn != null && !ap.def.apparel.CorrectGenderForWearing(pawn.gender))
            {
                num *= 0.01f;
            }
            return num;
        }
    }
}
