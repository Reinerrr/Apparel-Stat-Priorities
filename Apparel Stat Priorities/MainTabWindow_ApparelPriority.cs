using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ApparelStatPriorities
{
    public class MainTabWindow_ApparelPriority : MainTabWindow
    {
        public override Vector2 RequestedTabSize => new Vector2(1350f, 600f);

        private Vector2 scrollPosition = Vector2.zero;

        public static readonly List<string> ImportantStats = new List<string>
        {
            "WorkSpeedGlobal", "GeneralLaborSpeed", "MoveSpeed", "MedicalTendSpeed", "TradePriceImprovement", "SocialImpact",
            "AnimalGatherSpeed", "CookSpeed", "ConstructionSpeed", "PlantWorkSpeed", "MiningSpeed",
            "SmithingSpeed", "TailoringSpeed", "SmeltingSpeed", "DrugCookingSpeed",
            "EntityStudyRate", "ResearchSpeed"
        };

        private static readonly Dictionary<StatDef, SkillDef> StatToSkill = new Dictionary<StatDef, SkillDef>();

        static MainTabWindow_ApparelPriority()
        {
            StatDef cookSpeed = DefDatabase<StatDef>.GetNamedSilentFail("CookSpeed");
            if (cookSpeed != null) StatToSkill[cookSpeed] = SkillDefOf.Cooking;
            StatDef constructionSpeed = DefDatabase<StatDef>.GetNamedSilentFail("ConstructionSpeed");
            if (constructionSpeed != null) StatToSkill[constructionSpeed] = SkillDefOf.Construction;
            StatDef miningSpeed = DefDatabase<StatDef>.GetNamedSilentFail("MiningSpeed");
            if (miningSpeed != null) StatToSkill[miningSpeed] = SkillDefOf.Mining;
            StatDef medicalOperationSpeed = DefDatabase<StatDef>.GetNamedSilentFail("MedicalOperationSpeed");
            if (medicalOperationSpeed != null) StatToSkill[medicalOperationSpeed] = SkillDefOf.Medicine;
            StatDef medicalTendSpeed = DefDatabase<StatDef>.GetNamedSilentFail("MedicalTendSpeed");
            if (medicalTendSpeed != null) StatToSkill[medicalTendSpeed] = SkillDefOf.Medicine;
            StatDef plantWorkSpeed = DefDatabase<StatDef>.GetNamedSilentFail("PlantWorkSpeed");
            if (plantWorkSpeed != null) StatToSkill[plantWorkSpeed] = SkillDefOf.Plants;
            StatDef tradePrice = DefDatabase<StatDef>.GetNamedSilentFail("TradePriceImprovement");
            if (tradePrice != null) StatToSkill[tradePrice] = SkillDefOf.Social;
            StatDef socialImpact = DefDatabase<StatDef>.GetNamedSilentFail("SocialImpact");
            if (socialImpact != null) StatToSkill[socialImpact] = SkillDefOf.Social;
            StatDef animalGather = DefDatabase<StatDef>.GetNamedSilentFail("AnimalGatherSpeed");
            if (animalGather != null) StatToSkill[animalGather] = SkillDefOf.Animals;
            StatDef generalLaborSpeed = DefDatabase<StatDef>.GetNamedSilentFail("GeneralLaborSpeed");
            if (generalLaborSpeed != null) StatToSkill[generalLaborSpeed] = SkillDefOf.Crafting;
            StatDef smithingSpeed = DefDatabase<StatDef>.GetNamedSilentFail("SmithingSpeed");
            if (smithingSpeed != null) StatToSkill[smithingSpeed] = SkillDefOf.Crafting;
            StatDef tailoringSpeed = DefDatabase<StatDef>.GetNamedSilentFail("TailoringSpeed");
            if (tailoringSpeed != null) StatToSkill[tailoringSpeed] = SkillDefOf.Crafting;
            StatDef smeltingSpeed = DefDatabase<StatDef>.GetNamedSilentFail("SmeltingSpeed");
            if (smeltingSpeed != null) StatToSkill[smeltingSpeed] = SkillDefOf.Crafting;
            StatDef drugCookingSpeed = DefDatabase<StatDef>.GetNamedSilentFail("DrugCookingSpeed");
            if (drugCookingSpeed != null) StatToSkill[drugCookingSpeed] = SkillDefOf.Crafting;
            StatDef researchSpeed = DefDatabase<StatDef>.GetNamedSilentFail("ResearchSpeed");
            if (researchSpeed != null) StatToSkill[researchSpeed] = SkillDefOf.Intellectual;
            StatDef entityStudyRate = DefDatabase<StatDef>.GetNamedSilentFail("EntityStudyRate");
            if (entityStudyRate != null) StatToSkill[entityStudyRate] = SkillDefOf.Intellectual;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width - 350f, 40f), "ApparelStatPriorities_PriorityLabel".Translate());
            Text.Font = GameFont.Small;

            bool showOnly = ApparelStatPrioritiesSettings.showOnlyImportantStats;
            Widgets.CheckboxLabeled(new Rect(inRect.width - 380f, 5f, 260f, 30f), "ApparelStatPriorities_HideOtherStatsButton".Translate(), ref showOnly);
            if (showOnly != ApparelStatPrioritiesSettings.showOnlyImportantStats)
            {
                ApparelStatPrioritiesSettings.showOnlyImportantStats = showOnly;
                LoadedModManager.GetMod<ApparelStatPrioritiesMod>().WriteSettings();
            }

            if (Widgets.ButtonText(new Rect(inRect.width - 100f, 0f, 100f, 30f), "ApparelStatPriorities_CloseInterfaceButton".Translate()))
            {
                Close();
            }

            var manager = Current.Game.GetComponent<WorkRoleApparelManager>();
            if (manager == null || manager.trackedStats == null || manager.trackedStats.Count == 0)
            {
                Widgets.Label(new Rect(0f, 65f, inRect.width, 30f), "ApparelStatPriorities_LoadingWaiting".Translate());
                return;
            }

            var sortedStats = manager.trackedStats.OrderBy(s =>
            {
                int idx = ImportantStats.IndexOf(s.defName);
                return idx == -1 ? int.MaxValue : idx;
            }).ThenBy(s => s.label).ToList();

            var statsToDisplay = ApparelStatPrioritiesSettings.showOnlyImportantStats
                ? sortedStats.Where(s => ImportantStats.Contains(s.defName)).ToList()
                : sortedStats;

            float rowHeight = 30f;
            float headerHeight = 150f;
            float colNameWidth = 150f;

            float colStatWidth = 65f;

            float tableStartY = 30f;
            float bottomTextHeight = 30f;

            bool hasTooltip = false;
            string tooltipText = "";
            Rect tooltipRect = default(Rect);


            Rect fixedHeaderRect = new Rect(0f, tableStartY, colNameWidth, headerHeight);
            Widgets.DrawBoxSolid(fixedHeaderRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(fixedHeaderRect.ContractedBy(5f), "ApparelStatPriorities_Colonist".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            Rect scrollRect = new Rect(colNameWidth, tableStartY, inRect.width - colNameWidth, inRect.height - tableStartY - bottomTextHeight);
            Rect viewRect = new Rect(0f, 0f, statsToDisplay.Count * colStatWidth, headerHeight + (Find.CurrentMap.mapPawns.FreeColonistsSpawnedCount * rowHeight) + 10f);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float currentX = + 19f;
            for (int i = 0; i < statsToDisplay.Count; i++)
            {
                var stat = statsToDisplay[i];

                float textHeight = 60f;
                float textY = 25f;

                Rect textRect = new Rect(currentX, textY, colStatWidth, textHeight);


                DrawHeaderText(textRect, stat.label.CapitalizeFirst());


                if (Mouse.IsOver(new Rect(currentX, 0f, colStatWidth, headerHeight)))
                {
                    tooltipText = stat.label.CapitalizeFirst() + "\n\n" + stat.description;
                    tooltipRect = new Rect(currentX, 0f, colStatWidth, headerHeight);
                    hasTooltip = true;
                }

                currentX += colStatWidth;
            }

            float currentY = headerHeight + 5f;
            foreach (var pawn in Find.CurrentMap.mapPawns.FreeColonists)
            {
                currentX = + 25;
                foreach (var stat in statsToDisplay)
                {
                    Rect buttonRect = new Rect(currentX + (colStatWidth - 24f) / 2f, currentY + 3f, 24f, 24f);
                    DrawWeightButton(buttonRect, pawn, stat, manager);

                    if (Mouse.IsOver(buttonRect))
                    {
                        int weight = manager.GetWeight(pawn, stat);
                        tooltipText = "ApparelStatPriorities_PriorityCheckboxDescription".Translate(stat.label.CapitalizeFirst(), weight);
                        tooltipRect = buttonRect;
                        hasTooltip = true;
                    }

                    currentX += colStatWidth;
                }
                currentY += rowHeight;
            }

            Widgets.EndScrollView();

            if (hasTooltip)
            {
                Rect screenRect = new Rect(
                    scrollRect.x + tooltipRect.x - scrollPosition.x,
                    scrollRect.y + tooltipRect.y - scrollPosition.y,
                    tooltipRect.width,
                    tooltipRect.height
                );

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                TooltipHandler.TipRegion(screenRect, new TipSignal(tooltipText, tooltipText.GetHashCode()));
            }

            GUI.BeginGroup(new Rect(0f, tableStartY + headerHeight, colNameWidth, inRect.height - tableStartY - headerHeight));

            float nameY = 5f - scrollPosition.y;
            foreach (var pawn in Find.CurrentMap.mapPawns.FreeColonists)
            {
                if (nameY > -rowHeight && nameY < (scrollRect.height - headerHeight))
                {
                    Color originalColor = GUI.color;
                    if (pawn.IsSlave)
                    {
                        GUI.color = new Color32(199, 182, 36, 255);
                    }

                    Widgets.Label(new Rect(0f, nameY, colNameWidth, rowHeight), pawn.LabelShortCap);

                    GUI.color = originalColor;
                }
                nameY += rowHeight;
            }

            GUI.EndGroup();

            Color originalWarningColor = GUI.color;
            GUI.color = new Color32(176, 181, 187, 255);
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.LowerLeft;

            float footerY = inRect.height - bottomTextHeight + 5f;

            Widgets.Label(new Rect(0f, footerY, inRect.width, bottomTextHeight), "ApparelStatPriorities_Tips".Translate());

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = originalWarningColor;

        }

        private void DrawHeaderText(Rect rect, string text)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.LowerLeft;

            Text.WordWrap = true;

            Matrix4x4 matrix = GUI.matrix;

            Vector2 pivot = new Vector2(rect.x + rect.width / 2f, rect.yMax);

            GUIUtility.RotateAroundPivot(-60f, pivot);

            float expandedWidth = 150f;
            Rect wideRect = new Rect(
                pivot.x - (expandedWidth / 2f) + 25f,
                pivot.y - rect.height + 40f,
                expandedWidth,
                rect.height
            );

            Widgets.Label(wideRect, text);

            GUI.matrix = matrix;

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private void DrawWeightButton(Rect rect, Pawn pawn, StatDef stat, WorkRoleApparelManager manager)
        {
            int currentWeight = manager.GetWeight(pawn, stat);

            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.9f));

            if (StatToSkill.TryGetValue(stat, out SkillDef skill) && pawn.skills != null)
            {
                SkillRecord skillRecord = pawn.skills.GetSkill(skill);
                if (skillRecord != null)
                {
                    int skillLevel = skillRecord.Level;
                    Color borderColor = GetBorderColor(skillLevel);

                    GUI.color = borderColor;
                    GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), BaseContent.WhiteTex);
                    GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BaseContent.WhiteTex);
                    GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), BaseContent.WhiteTex);
                    GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), BaseContent.WhiteTex);
                    GUI.color = Color.white;

                    Passion passion = skillRecord.passion;
                    if (passion != Passion.None)
                    {
                        Texture2D flameTex = passion == Passion.Major
                            ? ContentFinder<Texture2D>.Get("UI/Icons/PassionMajor")
                            : ContentFinder<Texture2D>.Get("UI/Icons/PassionMinor");

                        if (flameTex != null)
                        {
                            Rect flameRect = new Rect(rect.xMax - 10f, rect.y - 2f, 12f, 12f);
                            GUI.DrawTexture(flameRect, flameTex);
                        }
                    }
                }
            }

            Color textColor = GetTextColor(currentWeight);
            Text.Font = GameFont.Medium;
            GUI.color = textColor;
            Text.Anchor = TextAnchor.MiddleCenter;
            string displayText = currentWeight == 5 ? "X" : currentWeight.ToString();
            Widgets.Label(rect, displayText);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonInvisible(rect))
            {
                if (Event.current.button == 0)
                {
                    currentWeight--;
                    if (currentWeight < 1) currentWeight = 5;
                }
                else if (Event.current.button == 1)
                {
                    currentWeight++;
                    if (currentWeight > 5) currentWeight = 1;
                }

                manager.SetWeight(pawn, stat, currentWeight);
                Event.current.Use();
            }
        }

        private Color GetTextColor(int weight)
        {
            switch (weight)
            {
                case 1: return new Color(0.5f, 0.8f, 0.5f);
                case 2: return new Color(0.65f, 0.9f, 0.65f);
                case 3: return new Color(0.8f, 0.95f, 0.8f);
                case 4: return new Color(0.95f, 1f, 0.95f);
                default: return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        private Color GetBorderColor(int level)
        {
            if (level == 0) return new Color32(88, 31, 100, 255);
            if (level == 1) return new Color32(88, 30, 104, 255);
            if (level == 2) return new Color32(87, 30, 101, 255);

            if (level >= 3)
            {
                float t = Mathf.Clamp01((level - 3) / 17f);
                Color startColor = new Color32(79, 83, 92, 255);
                Color endColor = new Color32(215, 204, 125, 255);
                return Color.Lerp(startColor, endColor, t);
            }
            return Color.clear;
        }
    }
}