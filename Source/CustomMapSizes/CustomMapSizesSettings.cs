namespace CustomMapSizes
{
    using Verse;

    public class CustomMapSizesSettings : ModSettings
    {
        // Adjust if you want different limits
        public const int Min = 101;

        public const int Max = 600;

        public int selectedMapSize = 250;

        public int customMapSizeHeight = 250;

        public int customMapSizeWidth = 250;

        public string customMapSizeHeightBuffer = "250";

        public string customMapSizeWidthBuffer = "250";

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref selectedMapSize, nameof(selectedMapSize), 250);
            Scribe_Values.Look(ref customMapSizeHeight, nameof(customMapSizeHeight), 250);
            Scribe_Values.Look(ref customMapSizeWidth, nameof(customMapSizeWidth), 250);
            Scribe_Values.Look(ref customMapSizeWidthBuffer, nameof(customMapSizeWidthBuffer), "250");
            Scribe_Values.Look(ref customMapSizeHeightBuffer, nameof(customMapSizeHeightBuffer), "250");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Clamp to sane bounds
                if (customMapSizeWidth < Min) customMapSizeWidth = Min;
                if (customMapSizeWidth > Max) customMapSizeWidth = Max;
                if (customMapSizeHeight < Min) customMapSizeHeight = Min;
                if (customMapSizeHeight > Max) customMapSizeHeight = Max;

                // Ensure buffers are non-null and reflect current ints
                if (string.IsNullOrEmpty(customMapSizeWidthBuffer))
                    customMapSizeWidthBuffer = customMapSizeWidth.ToString();
                if (string.IsNullOrEmpty(customMapSizeHeightBuffer))
                    customMapSizeHeightBuffer = customMapSizeHeight.ToString();
            }
        }
    }
}
