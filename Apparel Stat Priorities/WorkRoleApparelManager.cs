using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;

namespace ApparelStatPriorities
{
    public class PawnStatData : IExposable
    {
        public Pawn pawn;
        public Dictionary<StatDef, int> weights = new Dictionary<StatDef, int>();

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref weights, "weights", LookMode.Def, LookMode.Value);
        }
    }

    public class WorkRoleApparelManager : GameComponent
    {
        public Dictionary<Pawn, PawnStatData> allPawnData = new Dictionary<Pawn, PawnStatData>();
        public List<StatDef> trackedStats = new List<StatDef>();

        public WorkRoleApparelManager(Game game) : base()
        {
            CacheTrackedStats();
        }

        private void CacheTrackedStats()
        {
            var stats = new HashSet<StatDef>();
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (thingDef.apparel == null) continue;

                if (thingDef.statBases != null)
                {
                    foreach (var statBase in thingDef.statBases)
                        if (statBase.stat != null) stats.Add(statBase.stat);
                }

                if (thingDef.equippedStatOffsets != null)
                {
                    foreach (var offset in thingDef.equippedStatOffsets)
                        if (offset.stat != null) stats.Add(offset.stat);
                }
            }
            trackedStats = stats.OrderBy(s => s.label).ToList();
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref allPawnData, "allPawnData", LookMode.Reference, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (allPawnData == null)
                {
                    allPawnData = new Dictionary<Pawn, PawnStatData>();
                }
                else
                {
                    var nullKeys = allPawnData.Keys.Where(k => k == null).ToList();
                    foreach (var key in nullKeys)
                    {
                        allPawnData.Remove(key);
                    }
                }
            }
        }

        public int GetWeight(Pawn pawn, StatDef stat)
        {
            if (allPawnData != null && allPawnData.TryGetValue(pawn, out var data))
            {
                if (data.weights.TryGetValue(stat, out int weight))
                    return weight;
            }
            return 5;
        }

        public void SetWeight(Pawn pawn, StatDef stat, int weight)
        {
            if (allPawnData == null) allPawnData = new Dictionary<Pawn, PawnStatData>();

            if (!allPawnData.TryGetValue(pawn, out var data))
            {
                data = new PawnStatData { pawn = pawn };
                allPawnData[pawn] = data;
            }

            data.weights[stat] = weight;

            if (pawn.mindState != null)
            {
                pawn.mindState.nextApparelOptimizeTick = 0;
            }
        }
    }
}