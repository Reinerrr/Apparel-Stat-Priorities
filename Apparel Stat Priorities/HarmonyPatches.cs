using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;

namespace ApparelStatPriorities
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("Reiner.ApparelStatPriorities");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    public static class Patch_Pawn_JobTracker_TryTakeOrderedJob
    {
        public static void Prefix(Pawn_JobTracker __instance, Job job)
        {
            if (job == null) return;

            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn?.outfits?.forcedHandler == null) return;

            if (job.def == JobDefOf.Wear && job.targetA.Thing is Apparel newAp)
            {
                foreach (var worn in pawn.apparel.WornApparel)
                {
                    if (!ApparelUtility.CanWearTogether(newAp.def, worn.def, pawn.RaceProps.body))
                    {
                        if (pawn.outfits.forcedHandler.IsForced(worn))
                        {
                            pawn.outfits.forcedHandler.SetForced(worn, false);
                        }
                    }
                }
            }
            else if (job.def == JobDefOf.RemoveApparel && job.targetA.Thing is Apparel dropAp)
            {
                if (pawn.outfits.forcedHandler.IsForced(dropAp))
                {
                    pawn.outfits.forcedHandler.SetForced(dropAp, false);
                }
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "ApparelScoreGain")]
    public static class Patch_ApparelScoreGain
    {
        public static void Postfix(Pawn pawn, Apparel ap, ref float __result)
        {
            if (pawn.outfits?.forcedHandler != null)
            {
                foreach (var worn in pawn.apparel.WornApparel)
                {
                    if (pawn.outfits.forcedHandler.IsForced(worn) &&
                        !ApparelUtility.CanWearTogether(ap.def, worn.def, pawn.RaceProps.body))
                    {
                        __result = -9999f;
                        return;
                    }
                }
            }

            var manager = Current.Game.GetComponent<WorkRoleApparelManager>();
            if (manager == null || manager.trackedStats == null) return;

            float modGain = 0f;
            if (ap.def.equippedStatOffsets != null)
            {
                foreach (var offset in ap.def.equippedStatOffsets)
                {
                    if (offset?.stat == null) continue;
                    int weight = manager.GetWeight(pawn, offset.stat);

                    if (weight < 5)
                    {
                        modGain += offset.value * (float)Math.Pow(5f - weight, 3f) * 100f;
                    }
                }
            }
            foreach (var worn in pawn.apparel.WornApparel)
            {
                if (!ApparelUtility.CanWearTogether(ap.def, worn.def, pawn.RaceProps.body))
                {
                    if (worn.def.equippedStatOffsets != null)
                    {
                        foreach (var offset in worn.def.equippedStatOffsets)
                        {
                            if (offset?.stat == null) continue;
                            int weight = manager.GetWeight(pawn, offset.stat);

                            if (weight < 5)
                            {
                                modGain -= offset.value * (float)Math.Pow(5f - weight, 3f) * 100f;
                            }
                        }
                    }
                }
            }
            __result += modGain;
        }
    }

    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryGiveJob")]
    public static class Patch_TryGiveJob
    {
        private static readonly Func<Pawn, Apparel, float> ApparelScoreRaw =
            AccessTools.MethodDelegate<Func<Pawn, Apparel, float>>(
                AccessTools.Method(typeof(JobGiver_OptimizeApparel), "ApparelScoreRaw"));

        public static bool Prefix(JobGiver_OptimizeApparel __instance, Pawn pawn, ref Job __result)
        {
            if (pawn.Drafted) return true;
            if (!pawn.IsColonistPlayerControlled) return true;
            if (pawn.jobs?.curJob?.def == JobDefOf.Wear) return true;

            if (Find.TickManager.TicksGame < pawn.mindState.nextApparelOptimizeTick) return true;

            var manager = Current.Game.GetComponent<WorkRoleApparelManager>();
            if (manager == null || manager.trackedStats == null) return true;

            var activeWeights = new Dictionary<StatDef, int>();
            foreach (var stat in manager.trackedStats)
            {
                int weight = manager.GetWeight(pawn, stat);
                if (weight < 5)
                {
                    activeWeights[stat] = weight;
                }
            }


            Apparel bestApparel = null;
            float bestGain = 0.05f;

            foreach (Apparel ap in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
            {
                if (pawn.apparel.WornApparel.Contains(ap)) continue;
                if (!pawn.CanReserveAndReach(ap, PathEndMode.Touch, Danger.Deadly)) continue;
                if (!ap.PawnCanWear(pawn, true)) continue;
                if (ap.IsForbidden(pawn)) continue;
                if (pawn.outfits != null && !pawn.outfits.CurrentApparelPolicy.filter.Allows(ap)) continue;

                bool blockedByLock = false;
                if (pawn.outfits != null && pawn.outfits.forcedHandler != null)
                {
                    foreach (var worn in pawn.apparel.WornApparel)
                    {
                        if (pawn.outfits.forcedHandler.IsForced(worn) &&
                            !ApparelUtility.CanWearTogether(ap.def, worn.def, pawn.RaceProps.body))
                        {
                            blockedByLock = true;
                            break;
                        }
                    }
                }
                if (blockedByLock) continue;

                float rawScoreNew = ApparelScoreRaw?.Invoke(pawn, ap) ?? 0f;
                float rawScoreCurrent = 0f;
                foreach (var worn in pawn.apparel.WornApparel)
                {
                    if (!ApparelUtility.CanWearTogether(ap.def, worn.def, pawn.RaceProps.body))
                        rawScoreCurrent += ApparelScoreRaw?.Invoke(pawn, worn) ?? 0f;
                }
                float vanillaGain = rawScoreNew - rawScoreCurrent;

                float modScoreNew = CalculateModScore(ap, activeWeights);
                float modScoreCurrent = CalculateCurrentModScore(pawn, ap, activeWeights);
                float totalGain = vanillaGain + (modScoreNew - modScoreCurrent);

                if (totalGain > bestGain)
                {
                    bestGain = totalGain;
                    bestApparel = ap;
                }
            }

            if (bestApparel != null)
            {
                __result = JobMaker.MakeJob(JobDefOf.Wear, bestApparel);
                return false;
            }
            return true;
        }

        private static float CalculateModScore(Apparel ap, Dictionary<StatDef, int> activeWeights)
        {
            float score = 0f;
            if (ap.def.equippedStatOffsets == null) return score;

            foreach (var offset in ap.def.equippedStatOffsets)
            {
                if (offset?.stat == null) continue;

                if (activeWeights.TryGetValue(offset.stat, out int weight))
                {
                    if (offset.value > 0)
                        score += offset.value * (float)Math.Pow(5f - weight, 3f) * 100f;
                }
            }
            return score;
        }

        private static float CalculateCurrentModScore(Pawn pawn, Apparel newAp, Dictionary<StatDef, int> activeWeights)
        {
            float score = 0f;
            foreach (var worn in pawn.apparel.WornApparel)
            {
                if (!ApparelUtility.CanWearTogether(newAp.def, worn.def, pawn.RaceProps.body))
                    score += CalculateModScore(worn, activeWeights);
            }
            return score;
        }
    }

    [HarmonyPatch]
    public static class Patch_Pawn_ApparelTracker_TryDrop
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in typeof(Pawn_ApparelTracker).GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.Name != "TryDrop") continue;
                var ps = m.GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == typeof(Apparel))
                    yield return m;
            }
        }

        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Pawn_ApparelTracker __instance, object[] __args, ref bool __result)
        {
            if (__args.Length == 0 || !(__args[0] is Apparel ap)) return true;

            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.Dead) return true;
            if (pawn.outfits == null || pawn.outfits.forcedHandler == null) return true;
            if (!pawn.outfits.forcedHandler.IsForced(ap)) return true;

            var jobName = pawn?.jobs?.curJob?.def?.defName;
            if (jobName != null &&
                (jobName.Contains("ForceTargetWear") || jobName.Contains("OutfitStand")))
                return true;

            __result = false;
            for (int i = 1; i < __args.Length; i++)
                if (__args[i] == null || __args[i] is Apparel)
                    __args[i] = null;
            return false;
        }
    }

    [HarmonyPatch]
    public static class Patch_Pawn_ApparelTracker_Remove
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in typeof(Pawn_ApparelTracker).GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.Name != "Remove") continue;
                var ps = m.GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == typeof(Apparel))
                    yield return m;
            }
        }

        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Pawn_ApparelTracker __instance, object[] __args)
        {
            if (__args.Length == 0 || !(__args[0] is Apparel ap)) return true;

            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.Dead) return true;
            if (pawn.outfits == null || pawn.outfits.forcedHandler == null) return true;

            if (!pawn.outfits.forcedHandler.IsForced(ap)) return true;

            var jobName = pawn?.jobs?.curJob?.def?.defName;
            if (jobName == null || !jobName.Contains("Wear"))
                return true;

            return false;
        }
    }
}