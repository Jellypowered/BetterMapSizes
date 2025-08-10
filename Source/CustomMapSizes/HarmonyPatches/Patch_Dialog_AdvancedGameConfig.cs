namespace CustomMapSizes.HarmonyPatches
{
    using HarmonyLib;
    using RimWorld;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using UnityEngine;
    using Verse;

    [HarmonyPatch(typeof(Dialog_AdvancedGameConfig), "InitialSize", MethodType.Getter)]
    static class Patch_AdvancedGameConfig_InitialSize
    {
        static void Postfix(ref Vector2 __result)
        {
            const float leftColWidth = 250f;
            const float btnPadding = 16f;
            const float basePad = 20f;

            // Use saved mod settings, not GameInitData (which may be null/old here)
            var mod = LoadedModManager.GetMod<CustomMapSizesMain>();
            var s = mod?.settings;

            float customExtra = (s != null && s.selectedMapSize == -1) ? 60f : 0f;

            string btn = "CMS_MakeDefault".Translate();
            bool prevWrap = Text.WordWrap; Text.WordWrap = true;
            float btnTextH = Text.CalcHeight(btn, leftColWidth - btnPadding);
            Text.WordWrap = prevWrap;

            float btnExtra = Mathf.Max(0f, btnTextH - 24f);

            float extra = basePad + customExtra + btnExtra;
            __result.y = Mathf.Min(UI.screenHeight - 60f, __result.y + extra);
        }
    }

    [HarmonyPatch(typeof(Dialog_AdvancedGameConfig), nameof(Dialog_AdvancedGameConfig.DoWindowContents))]
    static class Patch_Dialog_AdvancedGameConfig_DoWindowContents
    {
        private static WeakReference _syncedDialog;

        private static float _baseHeight = -1f;

        static void Prefix(Dialog_AdvancedGameConfig __instance)
        {
            var mod = LoadedModManager.GetMod<CustomMapSizesMain>();
            var s = mod?.settings;
            if (s == null || Find.GameInitData == null) return;

            // Make the dialog resizable here (same effect as PreOpen)
            var resizeRef = AccessTools.FieldRefAccess<Window, bool>("resizeable");
            resizeRef(__instance) = true;

            bool alreadySynced = _syncedDialog != null && _syncedDialog.IsAlive &&
                                 ReferenceEquals(_syncedDialog.Target, __instance);
            if (alreadySynced) return;

            // Apply saved selection so vanilla radios highlight correctly
            Find.GameInitData.mapSize = s.selectedMapSize;
            if (s.selectedMapSize == -1) mod.CopyFromSettings(s);

            // Baseline for "Use saved default"
            CustomMapSizesMain.BaselineSelectedMapSize = s.selectedMapSize;
            CustomMapSizesMain.BaselineCustomW = s.customMapSizeWidth;
            CustomMapSizesMain.BaselineCustomH = s.customMapSizeHeight;
            CustomMapSizesMain.BaselineCaptured = true;

            // Capture baseline height
            var rectRef = AccessTools.FieldRefAccess<Window, Rect>("windowRect");
            Rect wr = rectRef(__instance);
            _baseHeight = wr.height;

            _syncedDialog = new WeakReference(__instance);
        }

        // Only adjust height during Repaint to avoid stealing focus from text fields.
        static void EnsureLiveHeight(float leftColumnWidth, bool customSelected, string buttonLabelUsed)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            if (_syncedDialog == null || !_syncedDialog.IsAlive) return;
            var dlg = _syncedDialog.Target as Window;
            if (dlg == null || _baseHeight <= 0f) return;

            const float btnPadding = 16f;
            const float basePad = 20f;

            float customExtra = customSelected ? 60f : 0f;

            bool prevWrap = Text.WordWrap; Text.WordWrap = true;
            float btnTextH = Text.CalcHeight(buttonLabelUsed, leftColumnWidth - btnPadding);
            Text.WordWrap = prevWrap;

            float btnExtra = Mathf.Max(0f, btnTextH - 24f);
            float extra = basePad + customExtra + btnExtra;
            float target = Mathf.Min(UI.screenHeight - 60f, _baseHeight + extra);

            var rectRef = AccessTools.FieldRefAccess<Window, Rect>("windowRect");
            Rect wr = rectRef(dlg);

            // Hysteresis: only move if there’s a noticeable difference
            const float eps = 1.5f;
            if (Mathf.Abs(wr.height - target) > eps)
            {
                wr.height = Mathf.Max(_baseHeight, target);
                rectRef(dlg) = wr;
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var target = AccessTools.Method(typeof(Listing), nameof(Listing.NewColumn));
            var hook = AccessTools.Method(typeof(Patch_Dialog_AdvancedGameConfig_DoWindowContents), nameof(NewColumnPlus));

            bool inserted = false;
            for (int i = 0; i < list.Count; i++)
            {
                var ins = list[i];
                if (!inserted &&
                    (ins.opcode == OpCodes.Callvirt || ins.opcode == OpCodes.Call) &&
                    ins.operand is MethodInfo mi && mi == target)
                {
                    // Move any labels on the call to the dup so branch targets remain valid
                    var dup = new CodeInstruction(OpCodes.Dup);
                    if (ins.labels != null && ins.labels.Count > 0)
                    {
                        dup.labels.AddRange(ins.labels);
                        ins.labels.Clear();
                    }

                    list.Insert(i, dup);                           // … listing (dup)
                    list.Insert(i + 1, new CodeInstruction(OpCodes.Call, hook)); // … call NewColumnPlus(listing)
                    inserted = true;
                    i += 1; // skip over the call we just inserted
                }
            }

            if (!inserted)
                Log.Warning("[CustomMapSizes] Did not inject NewColumnPlus; left column extras won't show.");

            return list;
        }

        // IMPORTANT: NewColumnPlus must NOT call listing.NewColumn() anymore (vanilla will, next).
        public static void NewColumnPlus(Listing listingBase)
        {
            var listing = listingBase as Listing_Standard;
            if (listing == null) { listingBase.NewColumn(); return; }

            var mod = LoadedModManager.GetMod<CustomMapSizesMain>();
            var s = (mod != null) ? mod.settings : null;

            int cur = (Find.GameInitData != null) ? Find.GameInitData.mapSize : 250;

            listing.Gap(10f);
            listing.Label("CMS_Custom".Translate());

            string customLabel = "CMS_CustomLabel".Translate(
                CustomMapSizesMain.mapWidth,
                CustomMapSizesMain.mapHeight,
                CustomMapSizesMain.mapWidth * CustomMapSizesMain.mapHeight);

            // If custom equals saved default, show Custom radio as OFF (visual only)
            bool customMatchesSaved =
                s != null &&
                cur == -1 &&
                s.selectedMapSize == -1 &&
                CustomMapSizesMain.mapWidth == s.customMapSizeWidth &&
                CustomMapSizesMain.mapHeight == s.customMapSizeHeight;

            bool isCustomSelected = (cur == -1) && !customMatchesSaved;

            if (listing.RadioButton(customLabel, isCustomSelected))
            {
                if (Find.GameInitData != null) Find.GameInitData.mapSize = -1;
                cur = -1;
            }

            // Show fields only when we're in custom AND it's different from saved default
            if (cur == -1 && !customMatchesSaved)
            {
                listing.Gap(5f);
                listing.TextFieldNumericLabeled("CMS_Width".Translate(),
                    ref CustomMapSizesMain.mapWidth, ref CustomMapSizesMain.mapWidthBuffer);
                listing.TextFieldNumericLabeled("CMS_Height".Translate(),
                    ref CustomMapSizesMain.mapHeight, ref CustomMapSizesMain.mapHeightBuffer);
            }

            listing.Gap(14f);
            if (s != null)
            {
                int savedSel = s.selectedMapSize;
                int savedW = (savedSel == -1) ? s.customMapSizeWidth : savedSel;
                int savedH = (savedSel == -1) ? s.customMapSizeHeight : savedSel;

                string defaultLabel = "CMS_UseSavedDefault".Translate(savedW, savedH, savedW * savedH);

                // Equals saved? (works for fixed and custom)
                bool equalsSaved =
                    Find.GameInitData != null &&
                    cur == savedSel &&
                    (savedSel != -1 ||
                     (CustomMapSizesMain.mapWidth == s.customMapSizeWidth &&
                      CustomMapSizesMain.mapHeight == s.customMapSizeHeight));

                // Keep "Use saved default" highlighted when we're already on the saved default
                bool isSavedSelected = equalsSaved;

                if (RadioButtonWrapped(listing, defaultLabel, isSavedSelected))
                {
                    if (Find.GameInitData != null) Find.GameInitData.mapSize = savedSel;
                    if (savedSel == -1)
                    {
                        CustomMapSizesMain.mapWidth = s.customMapSizeWidth;
                        CustomMapSizesMain.mapHeight = s.customMapSizeHeight;
                        CustomMapSizesMain.mapWidthBuffer = s.customMapSizeWidth.ToString();
                        CustomMapSizesMain.mapHeightBuffer = s.customMapSizeHeight.ToString();
                    }
                    cur = savedSel;
                    equalsSaved = true; // after reverting, we're equal
                }

                // ---- Make Default button (choose best label: long/short, wrapped/unwrapped) ----
                const float padding = 16f;
                float maxTextWidth = listing.ColumnWidth - padding;

                // Pick base labels depending on whether we're already at the saved default
                string longLbl = (!equalsSaved ? "CMS_IsNotDefault" : "CMS_MakeDefault").Translate();
                string shortLbl = (!equalsSaved ? "CMS_IsNotDefaultShort" : "CMS_MakeDefaultShort").Translate();

                // candidates: long (1 line), long (wrapped), short (1 line), short (wrapped)
                string candA = longLbl;
                string candB = WrapButtonLabelToTwoLines(longLbl, maxTextWidth);
                string candC = shortLbl;
                string candD = WrapButtonLabelToTwoLines(shortLbl, maxTextWidth);

                // measure each and pick the smallest height (prefer longer text on ties)
                bool prevWrap = Text.WordWrap; Text.WordWrap = true;
                float hA = Text.CalcHeight(candA, maxTextWidth);
                float hB = Text.CalcHeight(candB, maxTextWidth);
                float hC = Text.CalcHeight(candC, maxTextWidth);
                float hD = Text.CalcHeight(candD, maxTextWidth);
                Text.WordWrap = prevWrap;

                string bestLabel = candA;
                float bestH = hA;
                Action<string, float> consider = (txt, h) =>
                {
                    if (h + 0.01f < bestH || (Mathf.Abs(h - bestH) <= 0.01f && txt.Length > bestLabel.Length))
                    {
                        bestLabel = txt; bestH = h;
                    }
                };
                consider(candB, hB);
                consider(candC, hC);
                consider(candD, hD);

                float btnH = Mathf.Max(26f, bestH + 6f);
                Rect btnRect = listing.GetRect(btnH);

                // Tooltip
                if (!equalsSaved)
                    TooltipHandler.TipRegion(btnRect, "CMS_NotDefaultTip".Translate());
                else
                    TooltipHandler.TipRegion(btnRect, "CMS_AlreadyDefaultTip".Translate());

                // Color the button red when current selection is NOT the saved default
                Color prevColor = GUI.color;
                if (!equalsSaved) GUI.color = new Color(0.95f, 0.35f, 0.35f);

                if (Widgets.ButtonText(btnRect, bestLabel))
                {
                    s.selectedMapSize = cur;
                    if (cur == -1)
                    {
                        s.customMapSizeWidth = CustomMapSizesMain.mapWidth;
                        s.customMapSizeHeight = CustomMapSizesMain.mapHeight;
                        s.customMapSizeWidthBuffer = s.customMapSizeWidth.ToString();
                        s.customMapSizeHeightBuffer = s.customMapSizeHeight.ToString();
                    }
                    s.Write();
                    equalsSaved = true; // after saving, it's the default
                }

                GUI.color = prevColor; // restore color

                // live-resize based on current state + the actual label we drew (Repaint-only inside helper)
                EnsureLiveHeight(listing.ColumnWidth, cur == -1 && !customMatchesSaved, bestLabel);
            }
        }

        static string WrapButtonLabelToTwoLines(string text, float maxWidth)
        {
            // Fits already? keep single line
            var size = Text.CalcSize(text);
            if (size.x <= maxWidth) return text;

            // Try to split on spaces, aiming for balanced lines within maxWidth
            int bestBreak = -1;
            float bestScore = float.MaxValue;
            for (int i = 1; i < text.Length - 1; i++)
            {
                if (!char.IsWhiteSpace(text[i])) continue;
                string a = text.Substring(0, i);
                string b = text.Substring(i + 1);
                float wa = Text.CalcSize(a).x;
                float wb = Text.CalcSize(b).x;
                float over = Mathf.Max(0f, wa - maxWidth) + Mathf.Max(0f, wb - maxWidth);
                float balance = Mathf.Abs(wa - wb);
                float score = over * 1000f + balance; // prioritize fitting first, then balance
                if (score < bestScore) { bestScore = score; bestBreak = i; }
            }

            if (bestBreak > 0)
                return text.Substring(0, bestBreak) + "\n" + text.Substring(bestBreak + 1);

            // Fallback hard split near middle
            int mid = text.Length / 2;
            return text.Substring(0, mid) + "\n" + text.Substring(mid);
        }

        static bool RadioButtonWrapped(Listing_Standard listing, string label, bool selected)
        {
            const float radioSize = 24f;
            const float gap = 6f;

            float textWidth = listing.ColumnWidth - radioSize - gap;

            bool prevWrap = Text.WordWrap;
            Text.WordWrap = true;
            float textHeight = Text.CalcHeight(label, textWidth);
            float rowHeight = Mathf.Max(textHeight, radioSize);

            Rect row = listing.GetRect(rowHeight);
            Rect labelRect = new Rect(row.x, row.y, textWidth, rowHeight);
            Rect radioRect = new Rect(row.x + row.width - radioSize,
                                      row.y + (rowHeight - radioSize) / 2f,
                                      radioSize, radioSize);

            Widgets.Label(labelRect, label);

            // Click on the circle OR anywhere on the row
            bool clicked = Widgets.RadioButton(new Vector2(radioRect.x, radioRect.y), selected)
                           || Widgets.ButtonInvisible(row, true);

            if (clicked) Event.current.Use(); // swallow so other widgets don’t eat it
            Text.WordWrap = prevWrap;
            return clicked;
        }

        [HarmonyPatch(typeof(Page_SelectStartingSite), nameof(Page_SelectStartingSite.PostOpen))]
        public static class Patch_Page_SelectStartingSite_PostOpen
        {
            /// <summary>
            /// The Postfix.
            /// </summary>
            public static void Postfix()
            {
                var mod = LoadedModManager.GetMod<CustomMapSizesMain>();
                var settings = mod?.settings;
                if (settings == null || Find.GameInitData == null) return;

                Find.GameInitData.mapSize = settings.selectedMapSize;
                if (settings.selectedMapSize == -1)
                    mod.CopyFromSettings(settings); // loads saved custom W×H into statics
            }
        }
    }
}
