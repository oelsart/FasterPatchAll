using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using Verse;

namespace FasterPatchAll
{
    public class FasterPatchAll : Mod
    {
        public static FasterPatchAll Mod;

        public static Harmony Harmony;

        public readonly Settings Settings;

        public Dictionary<Assembly, Type[]> TypesByAssembly;

        public static Type[] AllHarmonyPatchTypes { get; private set; }

        private readonly Stopwatch stopwatch;

        public FasterPatchAll(ModContentPack content) : base(content)
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();
            Mod = this;
            Settings = GetSettings<Settings>();

            if (Settings.CacheTypesByAssembly)
            {
                TypesByAssembly = GenTypes.AllTypes.GroupBy(t => t.Assembly).ToDictionary(g => g.Key, g => g.ToArray());
            }
            if (Settings.EarlyFiltering)
            {
                AllHarmonyPatchTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("HarmonyLib.HarmonyPatch", false))
                    .Where(t => t != null)
                    .ToArray();
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
            if (Widgets.ButtonText(new Rect(inRect.width - 350f, 0f, 350f, 50f), "Log FasterPatchAll() Active Time"))
            {
                try
                {
                    Log.Message($"Stopwatch: {stopwatch.ElapsedMilliseconds} ms");
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
            listing_Standard.CheckboxLabeled("Early filtering to get patch classes.", ref Settings.EarlyFiltering);
            listing_Standard.End();
        }

        public static bool CanBeHarmonyClass(Type type)
        {
            if (type == null || type.ContainsGenericParameters)
            {
                return false;
            }
            return AllHarmonyPatchTypes?.Any(p => type.IsDefined(p)) ?? true;
        }

        public void Clear()
        {
            TypesByAssembly = null;
            Harmony.UnpatchAll("OELS.FasterPatchAll");
            stopwatch.Stop();
        }

        public override string SettingsCategory()
        {
            return "FasterPatchAll()";
        }

        public const int EealyFilterThreshold = 300;
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
            IEnumerable<Type> types = AccessTools.GetTypesFromAssembly(assembly);
            if (types.Count() > FasterPatchAll.EealyFilterThreshold)
            {
                types = types.Where(FasterPatchAll.CanBeHarmonyClass);
            }
            types.Do(type =>
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
            IEnumerable<Type> types = AccessTools.GetTypesFromAssembly(assembly);
            if (types.Count() > FasterPatchAll.EealyFilterThreshold)
            {
                types = types.Where(FasterPatchAll.CanBeHarmonyClass);
            }
            types.Where(type =>
            {
                var fromType = HarmonyMethodExtensions.GetFromType(type);
                var harmonyMethod = HarmonyMethod.Merge(fromType);
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
            IEnumerable<Type> types = AccessTools.GetTypesFromAssembly(assembly);
            if (types.Count() > FasterPatchAll.EealyFilterThreshold)
            {
                types = types.Where(FasterPatchAll.CanBeHarmonyClass);
            }
            types.Select(__instance.CreateClassProcessor).DoIf(patchClass => string.IsNullOrEmpty(patchClass.Category), patchClass =>
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
