using HarmonyLib; // Harmony
using KMod; // UserMod2
using System.Collections.Generic; // List, IComparer
using STRINGS;
using UnityEngine; // GameObject
using System.Reflection; // MethodInfo
using System.Runtime.CompilerServices; // MethodImpl
using Database; // SkillPerk, Skill

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
//   * a priority 5 task 15m away and a priority 6 task 30m away
//   * any otherwise equal-priority task within 13.6m of the duplicant
// 
// Navigation distance is valued linearly,
// while task and personal priority are valued exponentially relative to this.
// An increase of one task priority level is equivalent to halving distance.
// An increase of one personal priority level is equivalent to quartering distance.
// 
// Some consequences of this system:
//   * a dupe with "very low" task preference will value a priority 9 task equal
//       to a priority 1 task for which they have "very high" task preference.
//   * a dupe will value a priority 9 task 2560m away
//       equal to a priority 1 task 10m away.
//   * a dupe will value a priority 9 task 160m away
//       equal to a priority 5 task 10m away.
// 
// In general however, this should simply lead to sane, rational behaviour.

// TODO:
//   * adaptive storage priority based on filled / empty amount
//   * better emergency dupe selection
//   * reduced importance of storage tasks for tiny amounts of material
//   * importance reduction proportionate to wasted carrying capacity

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
        // game proximity setting is Game.Instance.advancedPersonalPriorities.
        //public static bool respectEnableProximity = false;
        
        // whether to display detailed debugging information in chore tooltips.
        public static bool debugTooltips = false;
        
        
        // heuristic function determining relative task importance.
        // as internally the value 0 is used to mean "do not ever do this",
        // the returned value here should always be non-negative.
        public static int TaskImportance(Chore.Precondition.Context task)
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
            
            // here we apply the basic heuristic.
            // ahh, the beauty of linear functions
            int total_prio = pref * prio;
            
            // navigation cost can be taken to be linear in distance.
            // we've already used 16 bits so we can use at maximum 14 here.
            // let's assume a minimum meaningful distance of 12.8 tiles,
            // and a maximum meaningful distance of 6553.6 tiles,
            // giving a spread of about 10 bits.
            if (task.cost >= 65536) { return total_prio; }
            if (task.cost < 136) { return total_prio * 1023; }
            // to match the more efficient cost calculation below,
            // we discard the bottom three bits of the cost.
            // this still gives sub-tile accuracy,
            // as cost is in units of 1/10 of a tile.
            int distance = task.cost >> 3;
            // to keep the result as an integer,
            // multiply by the maximum possible value then divide.
            total_prio *= (16383 / distance);
            // this gives a spread of 1 (65536+) to 1023 (135-).
            // it's not as pretty as it could be,
            // but the steps match those of the cost calc at the low end.
            
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
            
            // we don't really need sub-tile precision
            // so just eat the bottom 3 bits.
            // this lets us easily use 16 bits for distance cost,
            // giving a maximum meaningful distance of 6553.5 tiles,
            // and a minimum meaningful distance of 13.6 tiles.
            // 6.5km seems a reasonable upper limit for distance costs.
            if (task.cost < 136) { return (pref * prio) << 4; }
            if (task.cost >= 65536) { return (pref * prio) << 13; }
            // bits used: 1 + 8 + 8 + 13 = 30
            return pref * prio * (task.cost >> 3);
        }
        
        // simple version of task cost calc, for use in sort functions.
        // as this doesn't include duplicant preference,
        // it works for costs up to 2^^(31-9) or so.
        // as this corresponds to around 400,000 tiles
        // and current game maps only have 100,000 tiles in total,
        // this seems unlikely to ever be exceeded in practice.
        // as such, there is no check.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PrioCost(int prio, int cost)
        {
            if (cost < 128) { return (1 << (9 - prio)) << 7; }
            return (1 << (9 - prio)) * cost;
        }
        
        // i was going to use this but it turned out not to be necessary.
        // however, perhaps it will be useful again.
        public static bool IsDuplicant(GameObject obj)
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
    // fetch prioritization 1
    // ----------------------
    
    // which fetch task is better.
    // fetch chores seem to be pre-sorted into a single precondition.context,
    // so this needs to use the correct prioritization function.
    // it's also misnamed, it should be called "Subsumes".
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
            
            // here we override the cost function.
            // we can only take into account task priority,
            // as the chooser of the chore is not yet known.
            int thisCost = Util.PrioCost(__instance.priority.priority_value, __instance.cost);
            int fetchCost = Util.PrioCost(fetch.priority.priority_value, fetch.cost);
            if (thisCost != fetchCost)
            {
                __result = thisCost < fetchCost; return false;
            }
            
            // fall back to the default ordering in case of tie
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
    // fetch prioritization 2
    // ----------------------
    
    // not sure if modifying this is worth the slight extra calculation cost.
    // the IsBetterThan function above is always passed over the list once.
    // it's... not great if it's not in correct order,
    // but it will probably still technically work even if it disagrees.
    // ref: GlobalChoreProvider.UpdateFetches
    [HarmonyPatch]
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
            __result = b.priority.priority_class - a.priority.priority_class;
            if (__result != 0) { return false; }
            // using the same basic formula as above
            int aCost = Util.PrioCost(a.priority.priority_value, a.cost);
            int bCost = Util.PrioCost(b.priority.priority_value, b.cost);
            __result = aCost - bCost;
            return false;
        }
    }
    
    
    // ----------------------
    // fetch prioritization 3
    // ----------------------
    
    // this is a bit nasty because "ClearableManager" is internal.
    [HarmonyPatch]
    public class CollectSortedClearablesOverride
    {
        // we have to look this up, but that's okay
        static MethodInfo TargetMethod()
        {
            return AccessTools.TypeByName("ClearableManager")
                .GetMethod("CollectSortedClearables", BindingFlags.NonPublic | BindingFlags.Static);
        }
        
        // this structure must absolutely match that in ClearableManager
        private struct MarkedClearable
        {
            public Clearable clearable;
            public Pickupable pickupable;
            public Prioritizable prioritizable;
        }
        
        // this structure must absolutely match that in ClearableManager.
        // we override the comparer method.
        // this is kept simple by overloading the "cost" field,
        // such that it already incorporates task priority.
        private struct SortedClearable
        {
            public class Comparer : IComparer<SortedClearable>
            {
                public int Compare(SortedClearable a, SortedClearable b)
                {
                    return a.cost - b.cost;
                }
            }
            public Pickupable pickupable;
            public PrioritySetting masterPriority;
            public int cost;
            public static Comparer comparer = new Comparer();
        }
        
        // we are only overriding this so that we can override the cost.
        // it should be otherwise identical to base.
        static bool Prefix(
            Navigator navigator,
            KCompactedVector<MarkedClearable> clearables,
            List<SortedClearable> sorted_clearables
        ) {
            sorted_clearables.Clear();
            foreach (MarkedClearable data in clearables.GetDataList())
            {
                int navigationCost = data.pickupable.GetNavigationCost(navigator, data.pickupable.cachedCell);
                if (navigationCost != -1)
                {
                    PrioritySetting prio = data.prioritizable.GetMasterPriority();
                    sorted_clearables.Add(new SortedClearable
                    {
                        pickupable = data.pickupable,
                        masterPriority = prio,
                        // the only change: (previously just navigationCost)
                        cost = Util.PrioCost(prio.priority_value, navigationCost)
                    });
                }
            }
            sorted_clearables.Sort(SortedClearable.comparer);
            
            // we might theoretically want to reset the cost field here,
            // but it's not actually used anywhere in base code so why bother?
            
            return false; // skip original
        }
    }
    
    // --------------------------------
    // duplicant learned skill modifier
    // --------------------------------
    
    // apply a small modifier to priority,
    // for tasks requiring skilled labour.
    // currently this is +1 task priority per two skill levels.
    // this rounds to +1 priority for level 1 and 2 skills,
    // and +2 priority for level 3 and 4 skills.
    [HarmonyPatch(typeof(ChorePreconditions))]
    [HarmonyPatch(MethodType.Constructor)]
    public class DuplicantLearnedSkillModifier
    {
        public static void Postfix(
            ref ChorePreconditions __instance
        ) {
            __instance.HasSkillPerk.fn = delegate(
                ref Chore.Precondition.Context context,
                object data
            ) {
                // note: this is a resumé, not a resumption.
                MinionResume resume = context.consumerState.resume;
                if (!resume) { return false; }
                // tier will match the lowest dupe skill giving the perk
                int tier = int.MaxValue;
                // i'm pretty sure this is always going to be a HashedString,
                // but base code is like this, so...
                if (data is SkillPerk)
                {
                    SkillPerk perk = data as SkillPerk;
                    // resume.HasPerk
                    foreach (KeyValuePair<string, bool> item in resume.MasteryBySkillID)
                    {
                        Skill skill = Db.Get().Skills.Get(item.Key);
                        if (item.Value && skill.GivesPerk(perk))
                        {
                            if (skill.tier < tier) { tier = skill.tier; }
                        }
                    }
                }
                else if (data is HashedString)
                {
                    HashedString perkId = (HashedString)data;
                    // resume.HasPerk
                    foreach (KeyValuePair<string, bool> item in resume.MasteryBySkillID)
                    {
                        Skill skill = Db.Get().Skills.Get(item.Key);
                        if (item.Value && skill.GivesPerk(perkId))
                        {
                            if (skill.tier < tier) { tier = skill.tier; }
                        }
                    }
                }
                else if (data is string)
                {
                    HashedString perkId2 = (string)data;
                    // resume.HasPerk
                    foreach (KeyValuePair<string, bool> item in resume.MasteryBySkillID)
                    {
                        Skill skill = Db.Get().Skills.Get(item.Key);
                        if (item.Value && skill.GivesPerk(perkId2))
                        {
                            if (skill.tier < tier) { tier = skill.tier; }
                        }
                    }
                }
                else { return false; }
                if (tier == int.MaxValue) { return false; }
                // add the skill level to the priority directly.
                // tier 0 corresponds to learned skill level 1.
                // +0.5 priority per skill tier, rounded up.
                context.masterPriority.priority_value += (tier + 2) / 2;
                // clamp to the max priority so nothing unexpected happens
                if (context.masterPriority.priority_value > Chore.MAX_PLAYER_BASIC_PRIORITY)
                {
                    context.masterPriority.priority_value = Chore.MAX_PLAYER_BASIC_PRIORITY;
                }
                return true;
            };
        }
    }
    
    
    // ----------------------------
    // chore precondition overrides
    // ----------------------------
    
    // not sure if/when these actually apply to dupes...
    // when i messed with them critters stopped going to ranching tasks.
    // if it turns out they apply to dupes after all,
    // something will need to be done.
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
