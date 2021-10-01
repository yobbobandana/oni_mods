using HarmonyLib; // Harmony
using KMod; // UserMod2
using System.Reflection; // MethodInfo

namespace Template
{
    // this just does the default thing
    public class Template : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }
    
    // ------------
    // no suit wear
    // ------------
    [HarmonyPatch]
    public class SuitDurabilityPatch
    {
        static MethodInfo TargetMethod()
        {
            return typeof(Durability)
                .GetMethod("DeltaDurability", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        
        // whenever we try to reduce durability, reset it in stead.
        public static bool Prefix(ref float ___durability)
        {
            ___durability = 1f;
            return false;
        }
    }
}
