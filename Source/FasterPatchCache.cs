using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Verse;

namespace FasterPatchAll
{
    public class FasterPatchCache
    {
        public Dictionary<Assembly, Type[]> TypesByAssembly;

        public Type[] AllHarmonyAttributeTypes;

        public List<string> HarmonyFields;

        public AccessTools.FieldRef<HarmonyAttribute, HarmonyMethod> HarmonyAttribute_info;

        public Dictionary<FieldInfo, Func<HarmonyMethod, object>> Getters;

        public Dictionary<FieldInfo, Action<HarmonyMethod, object>> Setters;

        public FasterPatchCache()
        {
            if (FasterPatchAll.Mod.Settings.CacheTypesByAssembly)
            {
                TypesByAssembly = GenTypes.AllTypes.GroupBy(t => t.Assembly).ToDictionary(g => g.Key, g => g.ToArray());
            }
            if (FasterPatchAll.Mod.Settings.EarlyFiltering)
            {
                AllHarmonyAttributeTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name == "0Harmony" || a.FullName.StartsWith("0Harmony"))
                    .Select(a => a.GetType("HarmonyLib.HarmonyAttribute", false))
                    .Where(t => t != null)
                    .ToArray();
            }
            if (FasterPatchAll.Mod.Settings.ReflectionCache)
            {
                HarmonyAttribute_info = AccessTools.FieldRefAccess<HarmonyAttribute, HarmonyMethod>(nameof(HarmonyAttribute.info));
                HarmonyFields = (from s in AccessTools.GetFieldNames(typeof(HarmonyMethod))
                                 where s != "method"
                                 select s).ToList();
            }
            if (FasterPatchAll.Mod.Settings.HarmonyMethodTraverseCache)
            {
                Getters = new Dictionary<FieldInfo, Func<HarmonyMethod, object>>();
                Setters = new Dictionary<FieldInfo, Action<HarmonyMethod, object>>();
            }
        }

        public Func<HarmonyMethod, object> GetGetter(FieldInfo fieldInfo)
        {
            if (!Getters.TryGetValue(fieldInfo, out var getter))
            {
                var instanceParam = Expression.Parameter(typeof(HarmonyMethod), "instance");
                var fieldAccess = Expression.Field(instanceParam, fieldInfo);
                var castResult = Expression.Convert(fieldAccess, typeof(object));
                getter = Expression.Lambda<Func<HarmonyMethod, object>>(castResult, instanceParam).Compile();
                Getters[fieldInfo] = getter;
            }
            return getter;
        }

        public Action<HarmonyMethod, object> GetSetter(FieldInfo fieldInfo)
        {
            if (!Setters.TryGetValue(fieldInfo, out var setter))
            {
                var instanceParam = Expression.Parameter(typeof(HarmonyMethod), "instance");
                var valueParam = Expression.Parameter(typeof(object), "value");
                var castValue = Expression.Convert(valueParam, fieldInfo.FieldType);
                var fieldAccess = Expression.Field(instanceParam, fieldInfo);
                var assign = Expression.Assign(fieldAccess, castValue);
                setter = Expression.Lambda<Action<HarmonyMethod, object>>(assign, instanceParam, valueParam).Compile();
                Setters[fieldInfo] = setter;
            }
            return setter;
        }

        public void Clear()
        {
            TypesByAssembly = null;
            AllHarmonyAttributeTypes = null;
            HarmonyFields = null;
            HarmonyAttribute_info = null;
            Getters = null;
            Setters = null;
        }
    }
}
