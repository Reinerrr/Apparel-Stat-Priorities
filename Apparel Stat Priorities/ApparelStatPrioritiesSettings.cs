using Verse;

namespace ApparelStatPriorities
{
    public class ApparelStatPrioritiesSettings : ModSettings
    {
        public static bool showOnlyImportantStats = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showOnlyImportantStats, "showOnlyImportantStats", true);
        }
    }
}