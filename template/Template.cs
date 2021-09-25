using HarmonyLib; // Harmony
using KMod; // UserMod2

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
    
    // ---------------
    // some test patch
    // ---------------
    [HarmonyPatch(typeof(ClassToMod))]
    [HarmonyPatch("MethodName")]
    public class MyPatch
    {
        // constants and data can go here
        
        // Prefix code gets executed before the method
        public static void Prefix(ref ClassToMod __instance, arg1, arg2)
        {
        }
        
        // Postfix code gets executed after the method
        public static void Postfix(type __result)
        {
        }
    }
}
