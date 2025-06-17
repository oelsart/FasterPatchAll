using Verse;

namespace FasterPatchAll
{
    public class Settings : ModSettings
    {
        public bool CacheTypesByAssembly = true;

        public bool EarlyFiltering = false;

        public bool ReflectionCache = true;

        public bool HarmonyMethodTraverseCache = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref CacheTypesByAssembly, "CacheTypesByAssembly", true);
            Scribe_Values.Look(ref EarlyFiltering, "EarlyFiltering", false);
            Scribe_Values.Look(ref ReflectionCache, "ReflectionCache", true);
            Scribe_Values.Look(ref HarmonyMethodTraverseCache, "HarmonyMethodTraverseCache", true);
        }
    }
}
