namespace CustomMapSizes
{
    using HarmonyLib;
    using RimWorld;
    using System.Collections.Generic;
    using UnityEngine;
    using Verse;

    public class CustomMapSizesMain : Mod
    {
        public CustomMapSizesSettings settings;

        public static int mapHeight = 250;

        public static int mapWidth = 250;

        public static string mapHeightBuffer = "250";

        public static string mapWidthBuffer = "250";

        public static bool AppliedDefaultThisSession = false;

        // Baseline (captured once per page open)
        public static bool BaselineCaptured = false;

        public static int BaselineSelectedMapSize = 250;// -1 means custom

        public static int BaselineCustomW = 250;

        public static int BaselineCustomH = 250;

        // Optional: use translation key instead of hard string
        public override string SettingsCategory() => "CMS_ModName".Translate();// or "Custom Map Sizes"

        public CustomMapSizesMain(ModContentPack content) : base(content)
        {
            settings = GetSettings<CustomMapSizesSettings>();

            // Ensure statics reflect saved settings immediately
            CopyFromSettings(settings);

            var harmony = new Harmony($"{nameof(CustomMapSizes)}.{nameof(CustomMapSizesMain)}");
            harmony.PatchAll();
        }

        public void CopyFromSettings(CustomMapSizesSettings s)
        {
            mapHeight = s.customMapSizeHeight;
            mapWidth = s.customMapSizeWidth;
            mapHeightBuffer = s.customMapSizeHeightBuffer;
            mapWidthBuffer = s.customMapSizeWidthBuffer;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Top blurb
            var labelRect = inRect; labelRect.yMin += 30f;
            Widgets.Label(labelRect, "CMS_Settings_Description".Translate());

            var contentRect = labelRect; contentRect.yMin += 30f;

            // ---------- TWO COLUMNS ----------
            var twoCol = new Listing_Standard { ColumnWidth = (contentRect.width - 34f) / 2f };
            twoCol.Begin(contentRect);

            // Left: standard sizes
            Text.Font = GameFont.Medium;
            twoCol.Label("CMS_StandardSizes".Translate());
            Text.Font = GameFont.Small;

            var leftSizes = new List<int> { 200, 225, 250, 275, 300, 325 };
            foreach (int size in leftSizes)
            {
                var label = "MapSizeDesc".Translate(size, size * size);
                if (twoCol.RadioButton(label, settings.selectedMapSize == size))
                    settings.selectedMapSize = size;
            }

            // Right: “even tiles from center” (odd dims)
            twoCol.NewColumn();
            Text.Font = GameFont.Medium;
            twoCol.Label("CMS_CustomSizes".Translate());
            Text.Font = GameFont.Small;

            var rightSizes = new List<int> { 101, 151, 201, 251, 301, 351 };
            foreach (int size in rightSizes)
            {
                int radius = (size - 1) / 2; // e.g., 101 -> 50 tiles
                var baseLabel = "MapSizeDesc".Translate(size, size * size).ToString();
                var label = $"{baseLabel} — {radius} tiles from center";
                if (twoCol.RadioButton(label, settings.selectedMapSize == size))
                    settings.selectedMapSize = size;
            }

            float usedTwoColHeight = twoCol.CurHeight;
            twoCol.End();

            // ---------- DIVIDER ----------
            float yStart = contentRect.y + usedTwoColHeight + 8f;
            Widgets.DrawLineHorizontal(contentRect.x, yStart, contentRect.width);
            yStart += 8f;

            // ---------- CENTERED CUSTOM PANEL ----------
            // Desired content width (cap so it looks nice on very wide screens)
            float desiredContentW = Mathf.Min(520f, contentRect.width - 24f);
            float pad = 10f; // inner padding for the panel

            // Measure content height so we can draw the background first
            string customLabel = "CMS_CustomLabel".Translate(
                settings.customMapSizeWidth,
                settings.customMapSizeHeight,
                settings.customMapSizeWidth * settings.customMapSizeHeight);

            // Heights
            Text.Font = GameFont.Medium;
            float titleH = Text.CalcHeight("CMS_Custom".Translate(), desiredContentW);
            Text.Font = GameFont.Small;

            float radioH = CalcRadioRowHeight(customLabel, desiredContentW);
            float fieldsH = (settings.selectedMapSize == -1) ? 28f : 0f; // our side-by-side row
            float gaps = 2f /*title->radio*/ + ((fieldsH > 0f) ? 6f : 0f);
            float footH = Text.CalcHeight("CMS_PerfNote".Translate(), desiredContentW);
            float contentH = titleH + gaps + radioH + ((fieldsH > 0f) ? fieldsH + 6f : 0f) + 6f + footH;

            // Build rects (center horizontally)
            float totalW = desiredContentW + pad * 2f;
            float x = contentRect.x + (contentRect.width - totalW) / 2f;
            var bgRect = new Rect(x, yStart, totalW, contentH + pad * 2f);
            var innerRect = new Rect(bgRect.x + pad, bgRect.y + pad, desiredContentW, contentH);

            // Background: border + soft fill
            Widgets.DrawMenuSection(bgRect);
            Widgets.DrawLightHighlight(bgRect);

            // Draw content inside the centered panel
            var full = new Listing_Standard { ColumnWidth = innerRect.width };
            full.Begin(innerRect);

            Text.Font = GameFont.Medium;
            full.Label("CMS_Custom".Translate());
            Text.Font = GameFont.Small;

            if (RadioButtonWrapped(full, customLabel, settings.selectedMapSize == -1))
                settings.selectedMapSize = -1;

            if (settings.selectedMapSize == -1)
            {
                full.Gap(6f);
                DrawWidthHeightRow(full,
                    ref settings.customMapSizeWidth, ref settings.customMapSizeWidthBuffer,
                    ref settings.customMapSizeHeight, ref settings.customMapSizeHeightBuffer,
                    125, 600);
            }

            full.Gap(6f);
            var prev = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.6f);
            full.Label("CMS_PerfNote".Translate());
            GUI.color = prev;

            full.End();

            // Persist + keep globals in sync when Custom is the chosen default
            settings.Write();
            if (settings.selectedMapSize == -1)
                CopyFromSettings(settings);

            // Restore font
            Text.Font = GameFont.Small;
        }

        private static float CalcRadioRowHeight(string label, float availableWidth)
        {
            const float radioSize = 24f, gap = 6f;
            float textWidth = availableWidth - radioSize - gap;
            bool prevWrap = Text.WordWrap; Text.WordWrap = true;
            float textH = Text.CalcHeight(label, textWidth);
            Text.WordWrap = prevWrap;
            return Mathf.Max(textH, radioSize);
        }

        private static bool RadioButtonWrapped(Listing_Standard listing, string label, bool selected)
        {
            const float radioSize = 24f, gap = 6f;
            float textWidth = listing.ColumnWidth - radioSize - gap;

            bool prevWrap = Text.WordWrap; Text.WordWrap = true;
            float textHeight = Text.CalcHeight(label, textWidth);
            float rowHeight = Mathf.Max(textHeight, radioSize);

            Rect row = listing.GetRect(rowHeight);
            Rect labelRect = new Rect(row.x, row.y, textWidth, rowHeight);
            Rect radioRect = new Rect(row.x + row.width - radioSize,
                                      row.y + (rowHeight - radioSize) / 2f,
                                      radioSize, radioSize);

            Widgets.Label(labelRect, label);
            bool clicked = Widgets.RadioButton(new Vector2(radioRect.x, radioRect.y), selected)
                           || Widgets.ButtonInvisible(row, true);
            if (clicked) Event.current.Use();
            Text.WordWrap = prevWrap;
            return clicked;
        }

        private static void DrawWidthHeightRow(
            Listing_Standard listing,
            ref int width, ref string widthBuf,
            ref int height, ref string heightBuf,
            int min, int max)
        {
            float rowH = 28f;
            Rect row = listing.GetRect(rowH);

            const float labelW = 70f;
            const float fieldW = 100f;
            const float pad = 16f;

            Rect wLabel = new Rect(row.x, row.y, labelW, rowH);
            Rect wField = new Rect(wLabel.xMax, row.y, fieldW, rowH);
            Widgets.Label(wLabel, "CMS_Width".Translate());
            Widgets.TextFieldNumeric(wField, ref width, ref widthBuf, min, max);

            Rect hLabel = new Rect(wField.xMax + pad, row.y, labelW, rowH);
            Rect hField = new Rect(hLabel.xMax, row.y, fieldW, rowH);
            Widgets.Label(hLabel, "CMS_Height".Translate());
            Widgets.TextFieldNumeric(hField, ref height, ref heightBuf, min, max);
        }
    }
}
