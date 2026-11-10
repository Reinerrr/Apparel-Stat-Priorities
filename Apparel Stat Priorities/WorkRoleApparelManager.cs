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
            List<StatDef> statKeys = null;
            List<int> statValues = null;

            Scribe_Collections.Look(ref weights, "weights", LookMode.Def, LookMode.Value, ref statKeys, ref statValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && weights == null)
            {
                weights = new Dictionary<StatDef, int>();
            }
        }
    }

    public class WorkRoleApparelManager : GameComponent
    {
        public List<PawnStatData> allPawnData = new List<PawnStatData>();
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

            Scribe_Collections.Look(ref allPawnData, "allPawnData", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (allPawnData == null)
                {
                    allPawnData = new List<PawnStatData>();
                }
                else
                {
                    allPawnData.RemoveAll(data => data == null || data.pawn == null || data.pawn.Destroyed || data.pawn.Dead);
                }
            }
        }

        public int GetWeight(Pawn pawn, StatDef stat)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead) return 5;

            var data = allPawnData.FirstOrDefault(d => d.pawn == pawn);
            if (data != null && data.weights.TryGetValue(stat, out int weight))
            {
                return weight;
            }
            return 5;
        }

        public void SetWeight(Pawn pawn, StatDef stat, int weight)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead) return;
            if (allPawnData == null) allPawnData = new List<PawnStatData>();

            var data = allPawnData.FirstOrDefault(d => d.pawn == pawn);
            if (data == null)
            {
                data = new PawnStatData { pawn = pawn };
                allPawnData.Add(data);
            }

            data.weights[stat] = weight;

            if (pawn.mindState != null)
            {
                pawn.mindState.nextApparelOptimizeTick = 0;
            }
        }
    }
}