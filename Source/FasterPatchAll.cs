using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public FasterPatchCache Cache;

        private readonly Stopwatch stopwatch;

        public FasterPatchAll(ModContentPack content) : base(content)
        {
            stopwatch = Stopwatch.StartNew();
            Mod = this;
            Settings = GetSettings<Settings>();
            Cache = new FasterPatchCache();
            
            Harmony = new Harmony("OELS.FasterPatchAll");
            Harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var buttonWidth = inRect.width / 2f;
            if (Widgets.ButtonText(new Rect(0f, 0f, buttonWidth, 50f), "Log Harmony Patch Count"))
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
            if (Widgets.ButtonText(new Rect(inRect.width - buttonWidth, 0f, buttonWidth, 50f), "Log FasterPatchAll() Active Time"))
            {
                try
                {
                    Log.Message($"FasterPatchAll() Active Time: {stopwatch.ElapsedMilliseconds} ms");
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
            listing_Standard.CheckboxLabeled("Cache HarmonyFields.", ref Settings.ReflectionCache);
            listing_Standard.CheckboxLabeled("Cache accessor for HarmonyMethod.", ref Settings.HarmonyMethodTraverseCache);
            listing_Standard.End();
        }

        public bool CanBeHarmonyClass(Type type)
        {
            if (type == null || type.ContainsGenericParameters)
            {
                return false;
            }
            return Cache.AllHarmonyAttributeTypes?.Any(p => type.IsDefined(p)) ?? true;
        }

        public void Clear()
        {
            Harmony.UnpatchAll("OELS.FasterPatchAll");
            Cache.Clear();
            Cache = null;
            stopwatch.Stop();
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
            if (FasterPatchAll.Mod.Cache.TypesByAssembly?.TryGetValue(assembly, out __result) ?? false)
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
            AccessTools.GetTypesFromAssembly(assembly).DoIf(FasterPatchAll.Mod.CanBeHarmonyClass, type =>
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
            AccessTools.GetTypesFromAssembly(assembly).Where(FasterPatchAll.Mod.CanBeHarmonyClass)
                .Where(type =>
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
            AccessTools.GetTypesFromAssembly(assembly).Where(FasterPatchAll.Mod.CanBeHarmonyClass)
                .Select(__instance.CreateClassProcessor).DoIf(patchClass => string.IsNullOrEmpty(patchClass.Category), patchClass =>
                {
                    patchClass.Patch();
                });
            return false;
        }
    }

    [HarmonyPatch(typeof(HarmonyMethodExtensions), "GetHarmonyMethodInfo")]
    internal class Patch_GetHarmonyMethodInfo_GetHarmonyMethodInfo
    {
        static bool Prepare()
        {
            return FasterPatchAll.Mod.Settings.ReflectionCache;
        }

        static bool Prefix(object attribute, ref HarmonyMethod __result)
        {
            if (!(attribute is HarmonyAttribute harmonyAttribute))
            {
                __result = null;
                return false;
            }
            var cache = FasterPatchAll.Mod.Cache;
            var harmonyMethod = cache.HarmonyAttribute_info(harmonyAttribute);
            __result = AccessTools.MakeDeepCopy<HarmonyMethod>(harmonyMethod);
            return false;
        }
    }

    [HarmonyPatch(typeof(HarmonyMethod), nameof(HarmonyMethod.HarmonyFields))]
    internal class Patch_HarmonyMethod_HarmonyFields
    {
        static bool Prepare()
        {
            return FasterPatchAll.Mod.Settings.ReflectionCache;
        }

        static bool Prefix(ref List<string> __result)
        {
            __result = FasterPatchAll.Mod.Cache.HarmonyFields;
            return __result == null;
        }
    }

    [HarmonyPatch]
    internal class Patch_Traverse_GetValue
    {
        static bool Prepare()
        {
            return FasterPatchAll.Mod.Settings.HarmonyMethodTraverseCache;
        }

        static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(Traverse))
                .First(m => m.Name == "GetValue" && m.GetParameters().Length == 0 && !m.IsGenericMethodDefinition);
        }

        static bool Prefix(MemberInfo ____info, object ____root, ref object __result)
        {
            if (____root is HarmonyMethod harmonyMethod && ____info is FieldInfo fieldInfo)
            {
                var cache = FasterPatchAll.Mod.Cache;
                __result = cache.GetGetter(fieldInfo)(harmonyMethod);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Traverse), nameof(Traverse.SetValue))]
    internal class Patch_Traverse_SetValue
    {
        static bool Prepare()
        {
            return FasterPatchAll.Mod.Settings.HarmonyMethodTraverseCache;
        }

        static bool Prefix(MemberInfo ____info, object ____root, object value)
        {
            if (____root is HarmonyMethod harmonyMethod && ____info is FieldInfo fieldInfo)
            {
                var cache = FasterPatchAll.Mod.Cache;
                cache.GetSetter(fieldInfo)(harmonyMethod, value);
                return false;
            }
            return true;
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
