using HarmonyLib; // Harmony
using KMod; // UserMod2
//using System.Collections.Generic; // List
using STRINGS;
using UnityEngine; // GameObject
//using System.Reflection; // MethodInfo

// An attempt to make duplicant prioritization somewhat sane.
// 
// Dupe prioritization currently works something like this:
// 
// It will choose based on a strict priority heirarchy.
// At each level the highest priority task at that level is always preferred.
// Only when values are identical, will it consider the next type of priority.
//   1. Master Priority Class:
//       idle < basic < high < personal needs < top priority < compulsion
//       most player-selectable tasks are "basic", unless "!!" top priority.
//   2. Personal Priority: 0(disabled) to 5(very high)
//       this is the value set in the priority screen for the duplicant
//   3. Master Priority Value: 1 to 9 - the settable task priority value
//   4. Priority: 0 to 9999 - a preset internal priority per task type
//       if "enable proximity" is selected, this is set to 5000 for most tasks
//   5. PriorityMod: something related to fetch tasks, not sure if used at all
//   6. Consumer Priority: not sure what this is or if used at all
//   7. Navigation Cost: 10 * the travel distance to the job
// 
// This mod changes the system to work heuristically.
// The following priorities are taken to be of equal value:
//   * personal priority 1 and task priority 1
//   * personal priority 3 and task priority 5
//   * personal priority 5 and task priority 9
//   * a priority 5 task 12.8m away and a priority 6 task 25.6m away
//   * any otherwise equal-priority task within 12.8m of the duplicant
// 
// Navigation distance is valued linearly,
// while task and personal priority are valued exponentially relative to this.
// An increase of one task priority level is equivalent to halving distance.
// An increase of one personal priority level is equivalent to quartering distance.
// 
// Some consequences of this system:
//   * a dupe with "very low" task preference will value a priority 9 task equal
//       to a priority 1 task for which they have "very high" task preference.
//   * a dupe will value a priority 9 task 2560m away equal
//       to a priority 1 task 10m away.
//   * a dupe will value a priority 9 task 160m away equal
//       to a priority 5 task 10m away.
// 
// In general however, this should simply lead to sane, rational behaviour.

// TODO:
//   * rational storage priority
//   * better emergency dupe selection
//   * tooltips in errands tab for building need updating

// other included features:
//   * "out of world" dupes will not be listed in building errands

namespace RationalPriority
{
    // this just does the default thing
    public class RationalPriority : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }
    
    
    // ----------------
    // helper functions
    // ----------------
    // most importantly the one that calculates task priority
    
    public class Util
    {
        // whether or not to respect the global "enable proximity" setting.
        // if this is false, proximity is always taken into account.
        public static bool respectEnableProximity = false;
        
        // whether to display detailed debugging information in chore tooltips.
        public static bool debugTooltips = true;
        
        
        // heuristic function determining relative task importance.
        // as internally the value 0 is used to mean "do not ever do this",
        // the returned value here should always be non-negative.
        public static int TaskImportance(Chore.Precondition.Context task, bool debug = false)
        {
            // assume that priority zero means "do not ever do this"
            if (task.personalPriority <= 0) { return 0; }
            if (task.masterPriority.priority_value <= 0) { return 0; }
            
            // here we assume that the priority values given are exponential,
            // and that priority 9 is equally important as preference 5,
            // priority 1 is equally important as preference 1,
            // and priority 5 and preference 3 are the base values.
            // this can be achieved without explicit exponentiation by taking
            //   priority 1 = 2^^0 == 1
            //   priority 5 = 2^^4 == 16
            //   priority 9 = 2^^8 == 256
            //   pref 1 = 2^^0 == 1
            //   pref 3 = 2^^4 == 16
            //   pref 5 = 2^^8 == 256
            int pref = 1 << ((task.personalPriority - 1) * 2);
            int prio = 1 << (task.masterPriority.priority_value - 1);
            // what this means in practical terms
            // is that a dupe will travel twice as far to do a task with +1 priority level,
            // and 4 times as far to do a task they have +1 personal preference for.
            // this should all even out sensibly.
            
            // apply the basic heuristic.
            // ahh, the beauty of linear functions
            int total_prio = pref * prio;
            
            if (debug) { Debug.LogFormat("pref/prio/total : {0} / {1} / {2}", pref, prio, total_prio); }
            
            // maybe respect the "enable proximity" setting
            if (!respectEnableProximity || Game.Instance.advancedPersonalPriorities)
            {
                // navigation cost can be taken to be linear in distance.
                // assuming this never exceeds 2^^15 or so,
                // it should all work out.
                // let's also assume a minimum significant distance.
                // it's rather trivial to make that 12.8 tiles so why not.
                // internal "cost" here is approximately tile distance * 10.
                int distance = (task.cost >> 7) + 1;
                if (task.cost <= 0 || distance < 1) { distance = 1; }
                if (distance > 256) { distance = 256; }
                total_prio *= (256 / distance);
                if (debug) { Debug.LogFormat("dist/total_prio : {0} / {1}", distance, total_prio); }
            }
            // bits used: 8 + 8 + 10 = 26.
            return total_prio;
        }
        
        // this is effectively the same as the above function,
        // but expressed as a negative cost in stead of a positive importance.
        // this allows us to multiply distance in stead of dividing it,
        // often a significantly cheaper operation.
        public static int TaskCost(Chore.Precondition.Context task, bool debug = false)
        {
            // here we don't have an equivalent to "0" for "do not do this".
            // although we could use 0, it might interfere with comparisons.
            // therefore just use the max possible cost value.
            if (task.personalPriority <= 0) { return int.MaxValue; }
            if (task.masterPriority.priority_value <= 0) { return int.MaxValue; }
            
            // pref and prio are inverted
            int pref = 1 << ((5 - task.personalPriority) << 1);
            int prio = 1 << (9 - task.masterPriority.priority_value);
            
            if (!respectEnableProximity || Game.Instance.advancedPersonalPriorities)
            {
                // minimum meaningful distance of 12.8 tiles.
                // we could save some bits here by eating the bottom 7 bits,
                // but there's not really any need at the moment
                // and that does destroy precision.
                if (task.cost < 128) { return (pref * prio) << 7; }
                // pref and prio use up to 14 bits,
                // so ensure cost doesn't use more than 16.
                if (task.cost > 65536) { return (pref * prio) << 16; }
                return pref * prio * task.cost;
            }
            return pref * prio;
        }
        
        public bool IsDuplicant(GameObject obj)
        {
            return obj.HasTag(GameTags.DupeBrain);
        }
    }
    
    
    // -----------------------------------
    // modify the sort function for chores
    // -----------------------------------
    
    // this function is used to choose which task to do.
    // there is a pretrimmed list of tasks;
    // that list is sorted using this comparison function,
    // then the last valid element is selected.
    // ref: Chore.FindNextChore
    [HarmonyPatch(typeof(Chore.Precondition.Context))]
    [HarmonyPatch("CompareTo")]
    public class ChoreSortFunctionPatch
    {
        public static bool Prefix(
            Chore.Precondition.Context __instance,
            ref int __result,
            Chore.Precondition.Context obj
        ) {
            // note: always return false to override original method.
            
            // if one is allowed but not the other, the allowed one is better
            bool thisFailed = __instance.failedPreconditionId != -1;
            bool otherFailed = obj.failedPreconditionId != -1;
            // if the other failed preconditions, this is better
            if (otherFailed && !thisFailed) { __result = 1; return false; }
            // if this failed preconditions, the other is better
            if (thisFailed && !otherFailed) { __result = -1; return false; }
            
            // always prefer higher class tasks, as per standard behaviour.
            // task classes are those such as "idle", "personal needs" etc.
            __result = __instance.masterPriority.priority_class - obj.masterPriority.priority_class;
            if (__result != 0) { return false; }
            
            // in general, try to make a rational decision.
            // ...but not for special type tasks because sheesh, Klei.
            if (__instance.masterPriority.priority_class == PriorityScreen.PriorityClass.basic || __instance.masterPriority.priority_class == PriorityScreen.PriorityClass.high)
            {
                __result = Util.TaskCost(obj) - Util.TaskCost(__instance);
                if (__result != 0) { return false; }
            }
            
            // if that fails, do as usual
            __result = __instance.personalPriority - obj.personalPriority;
            if (__result != 0) { return false; }
            __result = __instance.masterPriority.priority_value - obj.masterPriority.priority_value;
            if (__result != 0) { return false; }
            __result = __instance.priority - obj.priority;
            if (__result != 0) { return false; }
            __result = __instance.priorityMod - obj.priorityMod;
            if (__result != 0) { return false; }
            __result = __instance.consumerPriority - obj.consumerPriority;
            if (__result != 0) { return false; }
            __result = obj.cost - __instance.cost;
            if (__result != 0) { return false; }
            if (__instance.chore == null && obj.chore == null) { __result = 0; return false; }
            if (__instance.chore == null) { __result = -1; return false; }
            if (obj.chore == null) { __result = 1; return false; }
            __result = __instance.chore.id - obj.chore.id;
            return false;
        }
    }
    
    
    // ----------------------
    // fetch prioritization 2
    // ----------------------
    
    // which fetch task is better.
    // fetch chores seem to be pre-sorted into a single precondition.context,
    // so this needs to use the correct prioritization function.
    [HarmonyPatch(typeof(GlobalChoreProvider.Fetch))]
    [HarmonyPatch("IsBetterThan")]
    public class FetchPriorityPatch
    {
        // completely overriding this as it probably runs frequently
        public static bool Prefix(
            GlobalChoreProvider.Fetch __instance,
            GlobalChoreProvider.Fetch fetch,
            ref bool __result
        ) {
            if (__instance.category != fetch.category)
            {
                __result = false; return false;
            }
            if (__instance.tagBitsHash != fetch.tagBitsHash)
            {
                __result = false; return false;
            }
            if (__instance.chore.choreType != fetch.chore.choreType)
            {
                __result = false; return false;
            }
            if (!__instance.chore.tagBits.AreEqual(ref fetch.chore.tagBits))
            {
                __result = false; return false;
            }
            if (__instance.priority.priority_class > fetch.priority.priority_class)
            {
                __result = true; return false;
            }
            if (__instance.priority.priority_class < fetch.priority.priority_class)
            {
                __result = false; return false;
            }
            // using the same formula as above,
            // just backwards so we can multiply in stead of divide the cost,
            // and simplified a little under the assumption that cost
            // will not exceed 2^^(31-9) or so
            int thisCost = 1 << (9 - __instance.priority.priority_value);
            int fetchCost = 1 << (9 - fetch.priority.priority_value);
            thisCost *= __instance.cost;
            fetchCost *= fetch.cost;
            if (thisCost != fetchCost)
            {
                __result = thisCost < fetchCost; return false;
            }
            // use the default ordering in case of ties
            if (__instance.priority.priority_value > fetch.priority.priority_value)
            {
                __result = true; return false;
            }
            if (__instance.priority.priority_value == fetch.priority.priority_value)
            {
                __result = __instance.cost <= fetch.cost; return false;
            }
            __result = false;
            return false;
        }
    }
    
    
    // ----------------------
    // fetch prioritization 1
    // ----------------------
    
    // not sure if modifying this is worth the slight extra calculation cost.
    // the IsBetterThan function above is always passed over the list once.
    // ref: GlobalChoreProvider.UpdateFetches
    /*[HarmonyPatch]
    public class FetchComparisonPatch
    {
        static MethodInfo TargetMethod()
        {
            return typeof(GlobalChoreProvider)
                .GetNestedType("FetchComparer", BindingFlags.NonPublic)
                .GetMethod("Compare");
        }
        
        // completely overriding this as it probably runs frequently
        public static bool Prefix(
            GlobalChoreProvider.Fetch a,
            GlobalChoreProvider.Fetch b,
            ref int __result
        ) {
            int num = b.priority.priority_class - a.priority.priority_class;
            if (num != 0)
            {
                __result = num; return false;
            }
            // using the same formula as above
            int aCost = 1 << (9 - a.priority.priority_value);
            int bCost = 1 << (9 - b.priority.priority_value);
            aCost *= a.cost;
            bCost *= b.cost;
            __result = aCost - bCost; return false;
        }
    }*/
    
    
    // ----------------------------
    // chore precondition overrides
    // ----------------------------
    
    // not sure when these actually apply to dupes...
    // when i messed with them critters stopped going to ranching tasks.
    /*
    [HarmonyPatch(typeof(ChorePreconditions))]
    [HarmonyPatch(MethodType.Constructor)]
    public class ChorePreconditionOverrides
    {
        public static void Postfix(ref ChorePreconditions __instance)
        {
            Debug.Log("Overriding chore preconditions");
            
            // this appears to be very important for critters
            __instance.IsMoreSatisfyingEarly = new Chore.Precondition
            {
                id = "IsMoreSatisfyingEarly",
                description = DUPLICANTS.CHORES.PRECONDITIONS.IS_MORE_SATISFYING,
                sortOrder = -2,
                fn = delegate(ref Chore.Precondition.Context context, object data)
                {
                    if (context.isAttemptingOverride)
                    {
                        return true;
                    }
                    if (context.skipMoreSatisfyingEarlyPrecondition)
                    {
                        return true;
                    }
                    if (context.consumerState.selectable.IsSelected)
                    {
                        return true;
                    }
                    Chore currentChore3 = context.consumerState.choreDriver.GetCurrentChore();
                    if (currentChore3 != null)
                    {
                        // for testing i'll say that if it's the wrong class,
                        // get rid of it. else keep it.
                        if (context.masterPriority.priority_class < currentChore3.masterPriority.priority_class)
                        {
                            return false;
                        }
                        return true;
                        // the rest is the default behaviour
                        //if (context.masterPriority.priority_class != currentChore3.masterPriority.priority_class)
                        //{
                        //    return context.masterPriority.priority_class > currentChore3.masterPriority.priority_class;
                        //}
                        //if (context.consumerState.consumer != null && context.personalPriority != context.consumerState.consumer.GetPersonalPriority(currentChore3.choreType))
                        //{
                        //    return context.personalPriority > context.consumerState.consumer.GetPersonalPriority(currentChore3.choreType);
                        //}
                        //if (context.masterPriority.priority_value != currentChore3.masterPriority.priority_value)
                        //{
                        //    return context.masterPriority.priority_value > currentChore3.masterPriority.priority_value;
                        //}
                        //return context.priority > currentChore3.choreType.priority;
                    }
                    return true;
                }
            };
            __instance.IsMoreSatisfyingLate = new Chore.Precondition
            {
                id = "IsMoreSatisfyingLate",
                description = DUPLICANTS.CHORES.PRECONDITIONS.IS_MORE_SATISFYING,
                sortOrder = 10000,
                fn = delegate(ref Chore.Precondition.Context context, object data)
                {
                    if (context.isAttemptingOverride)
                    {
                        return true;
                    }
                    if (!context.consumerState.selectable.IsSelected)
                    {
                        return true;
                    }
                    Chore currentChore2 = context.consumerState.choreDriver.GetCurrentChore();
                    if (currentChore2 != null)
                    {
                        // for testing i'll say that if it's the wrong class,
                        // get rid of it. else keep it.
                        if (context.masterPriority.priority_class < currentChore2.masterPriority.priority_class)
                        {
                            return false;
                        }
                        return true;
                        // the rest is the default behaviour
                        //if (context.masterPriority.priority_class != currentChore2.masterPriority.priority_class)
                        //{
                        //    return context.masterPriority.priority_class > currentChore2.masterPriority.priority_class;
                        //}
                        //if (context.consumerState.consumer != null && context.personalPriority != context.consumerState.consumer.GetPersonalPriority(currentChore2.choreType))
                        //{
                        //    return context.personalPriority > context.consumerState.consumer.GetPersonalPriority(currentChore2.choreType);
                        //}
                        //if (context.masterPriority.priority_value != currentChore2.masterPriority.priority_value)
                        //{
                        //    return context.masterPriority.priority_value > currentChore2.masterPriority.priority_value;
                        //}
                        //return context.priority > currentChore2.choreType.priority;
                    }
                    return true;
                }
            };
        }
    }*/
}