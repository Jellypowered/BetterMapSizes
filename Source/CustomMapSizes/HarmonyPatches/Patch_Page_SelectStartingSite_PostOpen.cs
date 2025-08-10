namespace CustomMapSizes
{
    using HarmonyLib;
    using RimWorld;
    using Verse;

    [HarmonyPatch(typeof(Page_SelectStartingSite), nameof(Page_SelectStartingSite.PostOpen))]
    public static class Patch_Page_SelectStartingSite_PostOpen
    {
        public static void Postfix()
        {
            var mod = LoadedModManager.GetMod<CustomMapSizesMain>();
            if (mod == null) return;

            var settings = mod.settings; // or mod.GetSettings<CustomMapSizesSettings>() if needed
            if (settings == null) return;

            if (Find.GameInitData == null) return;

            Find.GameInitData.mapSize = settings.selectedMapSize;

            mod.CopyFromSettings(settings); // keeps your width/height buffers in sync
        }
    }
}
