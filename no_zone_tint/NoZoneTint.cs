using HarmonyLib; // Harmony
using KMod; // UserMod2

namespace NoZoneTint
{
    // this just does the default thing for now
    public class NoZoneTint : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }
    
    // ---------------------------------------------------
    // set all zone tint colours to the same greyish white
    // ---------------------------------------------------
    
    [HarmonyPatch(typeof(SubworldZoneRenderData))]
    [HarmonyPatch("GenerateTexture")]
    public static class SubworldZoneRenderData_GenerateTexture_Patch
    {
        public static void Prefix(ref SubworldZoneRenderData __instance)
        {
            const int errorZone = 7;
            
            // note: default tints don't max out RGB channels.
            // some hit 235 in a single channel with very skewed tints.
            // set to 220/220/220 here to maintain approximate brightness.
            for (int i = 0; i < __instance.zoneColours.Length; i++)
            {
                if (i == errorZone) { continue; }
                __instance.zoneColours[i].r = 220;
                __instance.zoneColours[i].g = 220;
                __instance.zoneColours[i].b = 220;
            }
        }
    }
}
