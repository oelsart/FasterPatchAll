using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace FasterPatchAll
{
    public class FasterPatchAll : Mod
    {
        public static FasterPatchAll Mod;

        public static Harmony Harmony;

        public readonly Settings Settings;

        public Dictionary<Assembly, Type[]> TypesByAssembly;

        public FasterPatchAll(ModContentPack content) : base(content)
        {
            Mod = this;
            Settings = GetSettings<Settings>();

            if (Settings.CacheTypesByAssembly)
            {
                TypesByAssembly = GenTypes.AllTypes.GroupBy(t => t.Assembly).ToDictionary(g => g.Key, g => g.ToArray());
            }

            Harmony = new Harmony("OELS.FasterPatchAll");
            Harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (Widgets.ButtonText(new Rect(0f, 0f, 350f, 50f), "Log Harmony Patch Count"))
            {
                try
                {
                    Log.Message($"Harmony Patch Count: {Harmony.GetAllPatchedMethods().SelectMany(m => Harmony.GetPatchInfo(m).Owners).Count()}");
                }
                catch (Exception e)
                {
                    if (e != null)
                    {
                        Log.Error($"Failed to get harmony patch count: {e}");
                    }
                }
            }
            var listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect.BottomPartPixels(inRect.height - 50f));
            listing_Standard.CheckboxLabeled("Cache types by assembly.", ref Settings.CacheTypesByAssembly);
            listing_Standard.End();
        }

        public void Clear()
        {
            TypesByAssembly = null;
            Harmony.UnpatchAll("OELS.FasterPatchAll");
        }

        public override string SettingsCategory()
        {
            return "FasterPatchAll()";
        }
    }

    [HarmonyPatch(typeof(AccessTools), nameof(AccessTools.GetTypesFromAssembly))]
    internal class Patch_AccessTools_GetTypesFromAssembly
    {
        static bool Prepare()
        {
            return FasterPatchAll.Mod.Settings.CacheTypesByAssembly;
        }

        static bool Prefix(Assembly assembly, ref Type[] __result)
        {
            if (FasterPatchAll.Mod.TypesByAssembly?.TryGetValue(assembly, out __result) ?? false)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(StaticConstructorOnStartupUtility), nameof(StaticConstructorOnStartupUtility.CallAll))]
    internal class Patch_StaticConstructorOnStartupUtility_CallAll
    {
        static bool Prepare()
        {
            return FasterPatchAll.Mod.Settings.CacheTypesByAssembly;
        }

        static void Postfix()
        {
            LongEventHandler.ExecuteWhenFinished(FasterPatchAll.Mod.Clear);
        }
    }
}
