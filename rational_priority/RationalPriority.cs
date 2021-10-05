using HarmonyLib; // Harmony
using KMod; // UserMod2
using System.Collections.Generic; // List
using STRINGS;
using UnityEngine; // GameObject
using System.Reflection; // MethodInfo

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
    // this function is used to choose which task to do,
    // out of a pretrimmed list of tasks.
    // that list, is sorted using this comparison function,
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
    
    
    // --------------------------------------
    // chore tooltips for duplicant todo list
    // --------------------------------------
    // rewrites the chore tooltips for the adjusted priority system
    [HarmonyPatch(typeof(MinionTodoChoreEntry))]
    [HarmonyPatch("TooltipForChore")]
    public class ChoreTooltipsOverride
    {
        public static LocString TOOLTIP_IDLE = string.Concat(
            "{IdleDescription}\n\n",
            "Duplicants will only <b>{Errand}</b> when there is nothing else for them to do\n\n",
            "{BestGroup} priority: {TypePriority}"
        );

        public static LocString TOOLTIP_NORMAL = string.Concat(
            "{Description}\n\n",
            "Errand Type: {Groups}\n\n",
            "Total ", STRINGS.UI.PRE_KEYWORD, "Importance", STRINGS.UI.PST_KEYWORD, ": {TotalPriority}\n",
            "    • {Name}'s {BestGroup} Preference: {PersonalPriorityValue} ({PersonalPriority})\n",
            "    • This {Building}'s Importance: {BuildingPriorityValue} (Priority {BuildingPriority})\n",
            "    • Travel Cost: {TravelCost} ({TravelDistance}m)\n",
            "    • All {BestGroup} Errands: {TypePriority}\n\n",
            "Total Priority = ({PersonalPriorityValue} * {BuildingPriorityValue} * (256 / {TravelCost})) + {TypePriority} = {TotalPriority}"
        );

        public static LocString TOOLTIP_PERSONAL = string.Concat(
            "{Description}\n\n",
            "<b>{Errand}</b> is a ", STRINGS.UI.JOBSSCREEN.PRIORITY_CLASS.PERSONAL_NEEDS,
            " errand and so will be performed before all Regular errands\n\n",
            "{BestGroup} priority: {TypePriority}"
        );

        public static LocString TOOLTIP_EMERGENCY = string.Concat(
            "{Description}\n\n",
            "<b>{Errand}</b> is an ", STRINGS.UI.JOBSSCREEN.PRIORITY_CLASS.EMERGENCY,
            " errand and so will be performed before all Regular and Personal errands\n\n",
            "{BestGroup} priority: {TypePriority}"
        );

        public static LocString TOOLTIP_COMPULSORY = string.Concat(
            "{Description}\n\n",
            "<b>{Errand}</b> is a ", STRINGS.UI.JOBSSCREEN.PRIORITY_CLASS.COMPULSORY,
            " action and so will occur immediately\n\n",
            "{BestGroup} priority: {TypePriority}"
        );
        
        // this is the same as in the base code.
        // it's just private so duplicating it was the easiest thing to do...
        private static ChoreGroup BestPriorityGroup(Chore.Precondition.Context context, ChoreConsumer choreConsumer)
        {
            ChoreGroup choreGroup = null;
            if (context.chore.choreType.groups.Length != 0)
            {
                choreGroup = context.chore.choreType.groups[0];
                for (int i = 1; i < context.chore.choreType.groups.Length; i++)
                {
                    if (choreConsumer.GetPersonalPriority(choreGroup) < choreConsumer.GetPersonalPriority(context.chore.choreType.groups[i]))
                    {
                        choreGroup = context.chore.choreType.groups[i];
                    }
                }
            }
            return choreGroup;
        }
        
        public static bool Prefix(
            MinionTodoChoreEntry __instance,
            Chore.Precondition.Context context,
            ChoreConsumer choreConsumer,
            ref string __result
        ) {
            bool flag = context.chore.masterPriority.priority_class == PriorityScreen.PriorityClass.basic || context.chore.masterPriority.priority_class == PriorityScreen.PriorityClass.high;
            PriorityScreen.PriorityClass c = context.chore.masterPriority.priority_class;
            if (c == PriorityScreen.PriorityClass.idle) {
                __result = TOOLTIP_IDLE;
            } else if (c == PriorityScreen.PriorityClass.personalNeeds) {
                __result = TOOLTIP_PERSONAL;
            } else if (c == PriorityScreen.PriorityClass.topPriority) {
                __result = TOOLTIP_EMERGENCY;
            } else if (c == PriorityScreen.PriorityClass.compulsory) {
                __result = TOOLTIP_COMPULSORY;
            } else {
                __result = TOOLTIP_NORMAL;
            }
            __result = __result.Replace("{Description}", (context.chore.driver == choreConsumer.choreDriver) ? UI.UISIDESCREENS.MINIONTODOSIDESCREEN.TOOLTIP_DESC_ACTIVE : UI.UISIDESCREENS.MINIONTODOSIDESCREEN.TOOLTIP_DESC_INACTIVE);
            __result = __result.Replace("{IdleDescription}", (context.chore.driver == choreConsumer.choreDriver) ? UI.UISIDESCREENS.MINIONTODOSIDESCREEN.TOOLTIP_IDLEDESC_ACTIVE : UI.UISIDESCREENS.MINIONTODOSIDESCREEN.TOOLTIP_IDLEDESC_INACTIVE);
            __result = __result.Replace("{Name}", choreConsumer.name);
            __result = __result.Replace("{Errand}", GameUtil.GetChoreName(context.chore, context.data));
            string choreGroups = GameUtil.ChoreGroupsForChoreType(context.chore.choreType);
            __result = __result.Replace("{Groups}", choreGroups);
            ChoreGroup choreGroup = BestPriorityGroup(context, choreConsumer);
            __result = __result.Replace("{BestGroup}", (choreGroup != null) ? choreGroup.Name : context.chore.choreType.Name);
            int personalPriority = (flag ? choreConsumer.GetPersonalPriority(context.chore.choreType) : 0);
            __result = __result.Replace("{PersonalPriority}", JobsTableScreen.priorityInfo[personalPriority].name.text);
            __result = __result.Replace("{PersonalPriorityValue}", (1 << (personalPriority*2-2)).ToString());
            __result = __result.Replace("{Building}", context.chore.gameObject.GetProperName());
            int taskPriority = (flag ? context.chore.masterPriority.priority_value : 0);
            __result = __result.Replace("{BuildingPriorityValue}", (1 << (taskPriority-1)).ToString());
            __result = __result.Replace("{BuildingPriority}", taskPriority.ToString());
            float basePriority = (float)context.priority / 10000f;
            __result = __result.Replace("{TypePriority}", basePriority.ToString());
            int travelCost = (context.cost >> 7) + 1;
            if (travelCost < 1) { travelCost = 1; }
            __result = __result.Replace("{TravelCost}", travelCost.ToString());
            __result = __result.Replace("{TravelDistance}", ((float)context.cost/10.0f).ToString("#,0.#"));
            double totalPriority = (double)Util.TaskImportance(context) + (double)basePriority;
            __result = __result.Replace("{TotalPriority}", totalPriority.ToString("#,0.###"));
            
            // maybe tack on a bunch of debug info
            if (Util.debugTooltips) {
                __result += "\n\n" + DebugInfo(context, choreConsumer);
            }
            return false;
        }
        
        public static string DebugInfo(
            Chore.Precondition.Context context,
            ChoreConsumer choreConsumer
        ) {
            string text = "<b>Debug Info:</b>";
            text += string.Format("\nchore for {0}, type {1}",
                choreConsumer.GetProperName(),
                GameUtil.GetChoreName(context.chore, null)
            );
            text += string.Format("\nimportance: {0}", Util.TaskImportance(context));
            text += string.Format("\ncost: {0}", Util.TaskCost(context));
            text += string.Format("\npriorities: {0} | {1}/{2} | {3}/{4}/{5} | {6}",
                context.masterPriority.priority_class,
                context.personalPriority,
                context.masterPriority.priority_value,
                context.priority,
                context.priorityMod,
                context.consumerPriority,
                context.cost
            );
            // chore preconditions
            text += "\npreconditions:";
            List<Chore.PreconditionInstance> preconditions =
                context.chore.GetPreconditions();
            for (int i = 0; i < preconditions.Count; i++)
            {
                text += string.Format("\n  {0} - {1}",
                    preconditions[i].id,
                    preconditions[i].description
                );
            }
            // other available chores
            /*List<ChoreProvider> providers = choreConsumer.GetProviders();
            text += string.Format("\nchore providers: {0}", providers.Count);
            for (int i = 0; i < providers.Count; i++)
            {
                string prev_name = "";
                int count = 0;
                bool first = true;
                int drawn = 0;
                text += string.Format("\n  with {0} chores: ", providers[i].chores.Count);
                for (int j = 0; j < providers[i].chores.Count; j++)
                {
                    string name = GameUtil.GetChoreName(
                        providers[i].chores[j], null
                    );
                    if (name == prev_name) {
                        // add to previous
                        count++;
                        continue;
                    }
                    if (count == 1) {
                        if (!first) { text += ", "; }
                        text += prev_name;
                        first = false;
                    }
                    else if (count > 0) {
                        if (!first) { text += ", "; }
                        text += string.Format("{0} x {1}", prev_name, count);
                        first = false;
                    }
                    count = 1;
                    prev_name = name;
                    if (drawn++ > 10) {
                        text += "\n    ";
                        first = true;
                        drawn = 0;
                    }
                }
                if (count == 1) {
                    if (!first) { text += ", "; }
                    text += prev_name;
                }
                else if (count > 0) {
                    if (!first) { text += ", "; }
                    text += string.Format("{0} x {1}", prev_name, count);
                }
            }*/
            text += "\n";
            return text;
        }
    }
    
    
    // ----------------------
    // fetch prioritization 1
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
    
    
    
    // ----------------------
    // fetch prioritization 2
    // ----------------------
    // not yet sure if this is necessary. possiby not.
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
    
    
    // --------------------------
    // debug info for development
    // --------------------------
    /*
    [HarmonyPatch(typeof(ChoreConsumer))]
    [HarmonyPatch("FindNextChore")]
    public class DebugInfoPatch
    {
        // Postfix code gets executed after the method
        public static void Postfix(
            ref ChoreConsumer __instance,
            Chore.Precondition.Context out_context,
            bool __result,
            ChoreConsumer.PreconditionSnapshot ___preconditionSnapshot,
            List<ChoreProvider> ___providers
        ) {
            if (!__result) { return; }
            //if (!Util.IsDuplicant(__instance)) { return; }
            Debug.LogFormat("Found Chore for {0}, type {1}",
                __instance.GetProperName(),
                GameUtil.GetChoreName(out_context.chore, null)
            );
            Debug.LogFormat("  prio {0} | {1}/{2} | {3}/{4}/{5} | {6}",
                out_context.masterPriority.priority_class,
                out_context.personalPriority,
                out_context.masterPriority.priority_value,
                out_context.priority,
                out_context.priorityMod,
                out_context.consumerPriority,
                out_context.cost
            );
            //Debug.LogFormat("  prio class: {0}",
            //    out_context.masterPriority.priority_class);
            //Debug.LogFormat("  personal prio: {0}", out_context.personalPriority);
            //Debug.LogFormat("  master prio: {0}",
            //    out_context.masterPriority.priority_value);
            //Debug.LogFormat("  priority: {0}", out_context.priority);
            //Debug.LogFormat("  priorityMod: {0}", out_context.priorityMod);
            //Debug.LogFormat("  consumerPriority: {0}", out_context.consumerPriority);
            //Debug.LogFormat("  cost: {0}", out_context.cost);
            //Debug.LogFormat("  has solid transfer arm: {0}", __instance.consumerState.hasSolidTransferArm);
            Debug.LogFormat("  task importance: {0}",
                Util.TaskImportance(out_context, true));
            Debug.LogFormat("  {0} successful contexts",
                ___preconditionSnapshot.succeededContexts.Count
            );
            for (int i = ___preconditionSnapshot.succeededContexts.Count - 1;
                i >= 0; i--)
            {
                Chore.Precondition.Context context =
                    ___preconditionSnapshot.succeededContexts[i];
                Debug.LogFormat("    {0}: {1} ({2}/{3}/{4})",
                    GameUtil.GetChoreName(context.chore, null),
                    Util.TaskImportance(context),
                    context.personalPriority,
                    context.masterPriority.priority_value,
                    context.priority
                );
            }
            //Debug.LogFormat("  {0} failed contexts",
            //     ___preconditionSnapshot.failedContexts.Count);
            //for (int i = 0; i < ___preconditionSnapshot.failedContexts.Count; i++)
            //{
            //    Chore.Precondition.Context context =
            //        ___preconditionSnapshot.failedContexts[i];
            //    int id = context.failedPreconditionId;
            //    List<Chore.PreconditionInstance> precon =
            //        context.chore.GetPreconditions();
            //    Debug.LogFormat("    {0} ({1})",
            //        GameUtil.GetChoreName(context.chore, null),
            //        precon[id].id
            //    );
            //}
            Debug.LogFormat("  chore providers: {0}", ___providers.Count);
            for (int i = 0; i < ___providers.Count; i++)
            {
                string prev_name = "";
                int count = 0;
                Debug.LogFormat("    with {0} chores", ___providers[i].chores.Count);
                for (int j = 0; j < ___providers[i].chores.Count; j++)
                {
                    string name = GameUtil.GetChoreName(
                        ___providers[i].chores[j], null
                    );
                    if (name == prev_name) {
                        // add to previous
                        count++;
                        continue;
                    }
                    if (count == 1) { Debug.LogFormat("      {0}", prev_name); }
                    else if (count > 0) {
                        Debug.LogFormat("      {0} x {1}", prev_name, count);
                    }
                    count = 1;
                    prev_name = name;
                }
                if (count == 1) { Debug.LogFormat("      {0}", prev_name); }
                else if (count > 0) {
                    Debug.LogFormat("      {0} x {1}", prev_name, count);
                }
            }
            //Debug.Log("  preconditions on successful chore:");
            //List<Chore.PreconditionInstance> preconditions =
            //    out_context.chore.GetPreconditions();
            //for (int i = 0; i < preconditions.Count; i++)
            //{
            //    Debug.LogFormat("    {0} - {1}",
            //        preconditions[i].id,
            //        preconditions[i].description
            //    );
            //}
        }
    }*/
}
