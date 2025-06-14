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

        private HashSet<string> HarmonyAttributeNames;

        public FasterPatchAll(ModContentPack content) : base(content)
        {
            Mod = this;
            Settings = GetSettings<Settings>();

            HarmonyAttributeNames = typeof(HarmonyAttribute).AllSubclasses().Concat(typeof(HarmonyAttribute)).Select(a => a.FullName).ToHashSet();
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
                    Log.Message($"Harmony Patch Count: {Harmony.GetAllPatchedMethods().Count()}");
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
            listing_Standard.CheckboxLabeled("Early harmony class filtering.", ref Settings.EarlyFiltering);
            listing_Standard.End();
        }

        public void Clear()
        {
            TypesByAssembly = null;
            HarmonyAttributeNames = null;
            Harmony.UnpatchAll("OELS.FasterPatchAll");
        }

        public override string SettingsCategory()
        {
            return "FasterPatchAll()";
        }

        public static bool CanBeHarmonyClass(Type type)
        {
            return type?.CustomAttributes.Any(data => Mod.HarmonyAttributeNames?.Contains(data.AttributeType.FullName) ?? true) ?? false;
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

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAll), typeof(Assembly))]
    internal class Patch_Harmony_PatchAll
    {
        static bool Prepare()
        {
            return FasterPatchAll.Mod.Settings.EarlyFiltering;
        }

        static bool Prefix(Harmony __instance, Assembly assembly)
        {
            AccessTools.GetTypesFromAssembly(assembly)
                .Where(FasterPatchAll.CanBeHarmonyClass)
                .Do(type =>
                {
                __instance.CreateClassProcessor(type).Patch();
                });
            return false;
        }
    }


    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchCategory), typeof(Assembly), typeof(string))]
    internal class Patch_Harmony_PatchCategory
    {
        static bool Prepare()
        {
            return FasterPatchAll.Mod.Settings.EarlyFiltering;
        }

        static bool Prefix(Harmony __instance, Assembly assembly, string category)
        {
            AccessTools.GetTypesFromAssembly(assembly)
                .Where(FasterPatchAll.CanBeHarmonyClass)
                .Where(type =>
                {
                    List<HarmonyMethod> fromType = HarmonyMethodExtensions.GetFromType(type);
                    HarmonyMethod harmonyMethod = HarmonyMethod.Merge(fromType);
                    return harmonyMethod.category == category;
                }).Do(type =>
                {
                    __instance.CreateClassProcessor(type).Patch();
                });
            return false;
        }
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAllUncategorized), typeof(Assembly))]
    internal class Patch_Harmony_PatchAllUncategorized
    {
        static bool Prepare()
        {
            return FasterPatchAll.Mod.Settings.EarlyFiltering;
        }

        static bool Prefix(Harmony __instance, Assembly assembly)
        {
            AccessTools.GetTypesFromAssembly(assembly)
                .Where(FasterPatchAll.CanBeHarmonyClass)
                .Select(__instance.CreateClassProcessor)
                .DoIf(patchClass => string.IsNullOrEmpty(patchClass.Category), patchClass =>
                {
                    patchClass.Patch();
                });
            return false;
        }
    }

    [HarmonyPatch(typeof(StaticConstructorOnStartupUtility), nameof(StaticConstructorOnStartupUtility.CallAll))]
    internal class Patch_StaticConstructorOnStartupUtility_CallAll
    {
        static void Postfix()
        {
            LongEventHandler.ExecuteWhenFinished(FasterPatchAll.Mod.Clear);
        }
    }
}
