using UnityEngine;
using Verse;

namespace ApparelStatPriorities
{
    public class ApparelStatPrioritiesMod : Mod
    {
        public ApparelStatPrioritiesMod(ModContentPack content) : base(content)
        {
            GetSettings<ApparelStatPrioritiesSettings>();
        }

        public override string SettingsCategory() => "Apparel Stat Priorities";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
        }
    }
}