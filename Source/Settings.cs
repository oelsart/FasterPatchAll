using Verse;

namespace FasterPatchAll
{
    public class Settings : ModSettings
    {
        public bool CacheTypesByAssembly = true;

        public bool EarlyFiltering = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref CacheTypesByAssembly, "CacheTypesByAssembly", true);
            Scribe_Values.Look(ref EarlyFiltering, "EarlyFiltering", true);
        }
    }
}
