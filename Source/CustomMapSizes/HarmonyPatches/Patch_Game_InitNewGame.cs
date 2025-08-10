namespace CustomMapSizes.HarmonyPatches
{
    using HarmonyLib;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using Verse;

    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    internal static class Patch_Game_InitNewGame
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            var intVec3Ctor = AccessTools.Constructor(typeof(IntVec3),
                new[] { typeof(int), typeof(int), typeof(int) });

            // IMPORTANT: point to the helper method below
            var createMethod = AccessTools.Method(typeof(CmsIlHelpers), nameof(CmsIlHelpers.CreateCustomVector));

            int replacements = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                var ins = codes[i];
#pragma warning disable IDE0038
                if (ins.opcode == OpCodes.Call
                    && ins.operand is MethodBase
                    && (MethodBase)ins.operand == intVec3Ctor)
#pragma warning restore IDE0038
                {
                    // Look back for the ldloca.s that the ctor would write into
                    int j = i - 1;
                    int searchLimit = i - 12; if (searchLimit < 0) searchLimit = 0;

                    while (j >= searchLimit && codes[j].opcode != OpCodes.Ldloca_S) j--;

                    if (j >= searchLimit && codes[j].opcode == OpCodes.Ldloca_S)
                    {
                        object locOperand = codes[j].operand;

                        // Move labels/blocks from the removed ldloca.s onto the next instruction
                        if (j + 1 < codes.Count)
                        {
                            if (codes[j].labels != null && codes[j].labels.Count > 0)
                            {
                                codes[j + 1].labels.AddRange(codes[j].labels);
                                codes[j].labels.Clear();
                            }
                            if (codes[j].blocks != null && codes[j].blocks.Count > 0)
                            {
                                codes[j + 1].blocks.AddRange(codes[j].blocks);
                                codes[j].blocks.Clear();
                            }
                        }

                        // Remove ldloca.s
                        codes.RemoveAt(j);
                        i--;

                        // Replace ctor call with our static method (preserve labels/blocks)
                        var callReplacement = new CodeInstruction(OpCodes.Call, createMethod);
                        if (ins.labels != null && ins.labels.Count > 0)
                            callReplacement.labels.AddRange(ins.labels);
                        if (ins.blocks != null && ins.blocks.Count > 0)
                            callReplacement.blocks.AddRange(ins.blocks);

                        codes[i] = callReplacement;

                        // Store the returned IntVec3 into the same local
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Stloc_S, locOperand));

                        i++;
                        replacements++;
                    }
                }
            }

            if (replacements == 0)
                Log.Warning("[CustomMapSizes] No IntVec3 ctor sites were replaced in Game.InitNewGame.");

            return codes;
        }
    }

    // ← This is the “factory” method: a static helper the IL will call instead of new IntVec3(...)
    internal static class CmsIlHelpers
    {
        public static IntVec3 CreateCustomVector(int x, int y, int z)
        {
            if (x == -1 && z == -1)
            {
                var data = Find.GameInitData;
                if (data != null)
                {
                    if (data.mapSize == -1)
                        return new IntVec3(CustomMapSizesMain.mapWidth, y, CustomMapSizesMain.mapHeight);
                    if (data.mapSize > 0)
                        return new IntVec3(data.mapSize, y, data.mapSize);
                }
            }
            return new IntVec3(x, y, z);
        }
    }
}
