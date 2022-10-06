using HarmonyLib; // Harmony
using System.Reflection; // MethodInfo

namespace RationalPriority
{
    // --------------------------------------
    // hide out of world dupes in errands tab
    // --------------------------------------
    
    // note: this needs testing for how it behaves with rockets
    [HarmonyPatch]
    public class HideOutOfWorldDupesInErrandsPatch
    {
        public static MethodInfo TargetMethod()
        {
            return typeof(BuildingChoresPanel)
                .GetMethod("GetDupeEntry", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        
        public static bool Prefix(BuildingChoresPanel.DupeEntryData data)
        {
            // if the dupe is in another world, just skip them, geez
            if (!data.context.IsPotentialSuccess())
            {
                if (data.context.chore.GetPreconditions()[data.context.failedPreconditionId].id == "IsInMyParentWorld")
                {
                    return false;
                }
            }
            return true;
        }
    }
}
