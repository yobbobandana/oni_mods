using HarmonyLib; // Harmony
using STRINGS;
using System.Collections.Generic; // List

namespace RationalPriority
{
    // ----------------------
    // shared tooltip details
    // ----------------------
    
    // used by tooltips both in the todo list and the errands tab
    public class Tooltips
    {
        // A breakdown of the importance calculation for the given task.
        public static LocString DETAILS_BY_IMPORTANCE = string.Concat(
            "{Description}\n\n",
            "Errand Type: {Groups}\n\n",
            "Total ", STRINGS.UI.PRE_KEYWORD, "Task Importance", STRINGS.UI.PST_KEYWORD, ": {TotalPriority}\n",
            "    • {Name}'s {BestGroup} Preference: {PersonalPriorityValue} ({PersonalPriority})\n",
            "    • This {Building}'s Importance: {BuildingPriorityValue} (Priority {BuildingPriority})\n",
            "    • Errand Proximity: {ProximityValue} ({TravelDistance}m)\n",
            "    • All {BestGroup} Errands: {TypePriority}\n\n",
            "Total Importance = ({PersonalPriorityValue} × {BuildingPriorityValue} × {ProximityValue}) + {TypePriority} = {TotalPriority}"
        );
        
        // The same as above but framed as cost rather than importance.
        public static LocString DETAILS_BY_COST = string.Concat(
            "{Description}\n\n",
            "Errand Type: {Groups}\n\n",
            "Total ", STRINGS.UI.PRE_KEYWORD, "Task Cost", STRINGS.UI.PST_KEYWORD, ": {TotalCost}\n",
            "    • {Name}'s {BestGroup} Preference: {PersonalPriorityCost} ({PersonalPriority})\n",
            "    • This {Building}'s Importance: {BuildingPriorityCost} (Priority {BuildingPriority})\n",
            "    • Travel Cost: {TravelCost} ({TravelDistance}m)\n",
            "    • All {BestGroup} Errands: {TypeCost}\n\n",
            "Total Cost = ({PersonalPriorityCost} × {BuildingPriorityCost} × {TravelCost}) + {TypeCost} = {TotalCost}"
        );
        
        // Return a generic tooltip with detailed chore importance breakdown.
        // a "{Description}" tag will remain in the returned string,
        // but everything else will be filled out automatically.
        public static string ByImportance(
            Chore.Precondition.Context context,
            ChoreConsumer choreConsumer
        ) {
            string __result = DETAILS_BY_IMPORTANCE;
            __result = __result.Replace("{Name}", choreConsumer.name);
            __result = __result.Replace("{Errand}", GameUtil.GetChoreName(context.chore, context.data));
            string choreGroups = GameUtil.ChoreGroupsForChoreType(context.chore.choreType);
            __result = __result.Replace("{Groups}", choreGroups);
            ChoreGroup choreGroup = BestPriorityGroup(context, choreConsumer);
            __result = __result.Replace("{BestGroup}", (choreGroup != null) ? choreGroup.Name : context.chore.choreType.Name);
            int personalPriority = choreConsumer.GetPersonalPriority(context.chore.choreType);
            __result = __result.Replace("{PersonalPriority}", JobsTableScreen.priorityInfo[personalPriority].name.text);
            __result = __result.Replace("{PersonalPriorityValue}", (1 << (personalPriority*2-2)).ToString());
            __result = __result.Replace("{Building}", context.chore.gameObject.GetProperName());
            int taskPriority = context.masterPriority.priority_value;
            string buildingPriority = taskPriority.ToString();
            bool hasSkillModifier = HasSkillPerkPriorityModifier(context);
            if (hasSkillModifier) { buildingPriority += "*"; }
            __result = __result.Replace("{BuildingPriorityValue}", (1 << (taskPriority-1)).ToString());
            __result = __result.Replace("{BuildingPriority}", buildingPriority);
            float basePriority = (float)context.priority / 10000f;
            __result = __result.Replace("{TypePriority}", basePriority.ToString());
            // this follows the cost calc, and is thus a bit ugly
            int proximityValue;
            if (context.cost < 136) { proximityValue = 1023; }
            else if (context.cost >= 65536) { proximityValue = 1; }
            else { proximityValue = 16383 / (context.cost >> 3); }
            __result = __result.Replace("{ProximityValue}", proximityValue.ToString());
            __result = __result.Replace("{TravelDistance}", ((float)context.cost/10.0f).ToString("#,0.#"));
            double totalPriority = (double)Util.TaskImportance(context) + (double)basePriority;
            __result = __result.Replace("{TotalPriority}", totalPriority.ToString("#,0.###"));
            if (hasSkillModifier) {
                __result += "\n\n* This task's learned skill requirement slightly increases its priority for skilled duplicants.";
            }
            return __result;
        }
        
        // Return a generic tooltip with detailed chore cost breakdown.
        // A "{Description}" tag will remain in the returned string,
        // but everything else will be filled out automatically.
        public static string ByCost(
            Chore.Precondition.Context context,
            ChoreConsumer choreConsumer
        ) {
            string __result = DETAILS_BY_COST;
            __result = __result.Replace("{Name}", choreConsumer.name);
            __result = __result.Replace("{Errand}", GameUtil.GetChoreName(context.chore, context.data));
            string choreGroups = GameUtil.ChoreGroupsForChoreType(context.chore.choreType);
            __result = __result.Replace("{Groups}", choreGroups);
            ChoreGroup choreGroup = BestPriorityGroup(context, choreConsumer);
            __result = __result.Replace("{BestGroup}", (choreGroup != null) ? choreGroup.Name : context.chore.choreType.Name);
            int personalPriority = choreConsumer.GetPersonalPriority(context.chore.choreType);
            __result = __result.Replace("{PersonalPriority}", JobsTableScreen.priorityInfo[personalPriority].name.text);
            __result = __result.Replace("{PersonalPriorityCost}", (1 << ((5 - personalPriority) << 1)).ToString());
            __result = __result.Replace("{Building}", context.chore.gameObject.GetProperName());
            int taskPriority = context.masterPriority.priority_value;
            string buildingPriority = taskPriority.ToString();
            bool hasSkillModifier = HasSkillPerkPriorityModifier(context);
            if (hasSkillModifier) { buildingPriority += "*"; }
            __result = __result.Replace("{BuildingPriorityCost}", (1 << (9 -taskPriority)).ToString());
            __result = __result.Replace("{BuildingPriority}", buildingPriority);
            float basePriority = (float)context.priority / 10000f;
            __result = __result.Replace("{TypeCost}", (1f - basePriority).ToString());
            int travelCost = context.cost;
            if (travelCost < 128) { travelCost = 16; }
            else if (travelCost >= 65536) { travelCost = 8192; }
            else { travelCost >>= 3; }
            __result = __result.Replace("{TravelCost}", travelCost.ToString());
            __result = __result.Replace("{TravelDistance}", ((float)context.cost/10.0f).ToString("#,0.#"));
            double totalCost = (double)Util.TaskCost(context) + 1d - (double)basePriority;
            __result = __result.Replace("{TotalCost}", totalCost.ToString("#,0.###"));
            if (hasSkillModifier) {
                __result += "\n\n* This task's learned skill requirement slightly increases its priority for skilled duplicants.";
            }
            return __result;
        }
        
        // whether priority has been modified by learned skill importance
        public static bool HasSkillPerkPriorityModifier(
            Chore.Precondition.Context context
        ) {
            List<Chore.PreconditionInstance> preconditions =
                context.chore.GetPreconditions();
            for (int i = 0; i < preconditions.Count; i++)
            {
                if (preconditions[i].id == "HasSkillPerk")
                {
                    return true;
                }
            }
            return false;
        }
        
        // this is the same as in the base code.
        // it's just private so duplicating it was the easiest thing to do...
        // ref: MinionTodoChoreEntry
        public static ChoreGroup BestPriorityGroup(
            Chore.Precondition.Context context,
            ChoreConsumer choreConsumer)
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
    }
    
    
    // --------------------------------------
    // chore tooltips for duplicant todo list
    // --------------------------------------
    
    // rewrites the chore tooltips for the adjusted priority system
    [HarmonyPatch(typeof(MinionTodoChoreEntry))]
    [HarmonyPatch("TooltipForChore")]
    public class ChoreTooltipsOverride
    {
        // "Idle" clss errands (basically just Idle)
        public static LocString TOOLTIP_IDLE = string.Concat(
            "{IdleDescription}\n\n",
            "Duplicants will only <b>{Errand}</b> when there is nothing else for them to do\n\n",
            "{BestGroup} priority: {TypePriority}"
        );
        
        // "Basic" and "High" class errands will use the shared tooltips above.
        
        // "Personal Needs" class errands (such as Eat)
        public static LocString TOOLTIP_PERSONAL = string.Concat(
            "{Description}\n\n",
            "<b>{Errand}</b> is a ", STRINGS.UI.JOBSSCREEN.PRIORITY_CLASS.PERSONAL_NEEDS,
            " errand and so will be performed before all Regular errands\n\n",
            "{BestGroup} priority: {TypePriority}"
        );
        
        // "Top Priority" (emergency / yellow alert) class errands
        public static LocString TOOLTIP_EMERGENCY = string.Concat(
            "{Description}\n\n",
            "<b>{Errand}</b> is an ", STRINGS.UI.JOBSSCREEN.PRIORITY_CLASS.EMERGENCY,
            " errand and so will be performed before all Regular and Personal errands\n\n",
            "{BestGroup} priority: {TypePriority}"
        );
        
        // "Compulsory" class errands (such as Move To)
        public static LocString TOOLTIP_COMPULSORY = string.Concat(
            "{Description}\n\n",
            "<b>{Errand}</b> is a ", STRINGS.UI.JOBSSCREEN.PRIORITY_CLASS.COMPULSORY,
            " action and so will occur immediately\n\n",
            "{BestGroup} priority: {TypePriority}"
        );
        
        public static bool Prefix(
            //MinionTodoChoreEntry __instance,
            Chore.Precondition.Context context,
            ChoreConsumer choreConsumer,
            ref string __result
        ) {
            PriorityScreen.PriorityClass c = context.chore.masterPriority.priority_class;
            if (c == PriorityScreen.PriorityClass.idle) {
                __result = TOOLTIP_IDLE;
            } else if (c == PriorityScreen.PriorityClass.personalNeeds) {
                __result = TOOLTIP_PERSONAL;
            } else if (c == PriorityScreen.PriorityClass.topPriority) {
                __result = TOOLTIP_EMERGENCY;
            } else if (c == PriorityScreen.PriorityClass.compulsory) {
                __result = TOOLTIP_COMPULSORY;
            } else { // basic || high
                // the bulk of the info comes from the shared method here
                __result = Tooltips.ByImportance(context, choreConsumer);
            }
            __result = __result.Replace("{Description}", (context.chore.driver == choreConsumer.choreDriver) ? UI.UISIDESCREENS.MINIONTODOSIDESCREEN.TOOLTIP_DESC_ACTIVE : UI.UISIDESCREENS.MINIONTODOSIDESCREEN.TOOLTIP_DESC_INACTIVE);
            __result = __result.Replace("{IdleDescription}", (context.chore.driver == choreConsumer.choreDriver) ? UI.UISIDESCREENS.MINIONTODOSIDESCREEN.TOOLTIP_IDLEDESC_ACTIVE : UI.UISIDESCREENS.MINIONTODOSIDESCREEN.TOOLTIP_IDLEDESC_INACTIVE);
            // {Name} and {Errand} can also appear in the filled {Description}
            __result = __result.Replace("{Name}", choreConsumer.name);
            __result = __result.Replace("{Errand}", GameUtil.GetChoreName(context.chore, context.data));
            ChoreGroup choreGroup = Tooltips.BestPriorityGroup(context, choreConsumer);
            __result = __result.Replace("{BestGroup}", (choreGroup != null) ? choreGroup.Name : context.chore.choreType.Name);
            float basePriority = (float)context.priority / 10000f;
            __result = __result.Replace("{TypePriority}", basePriority.ToString());
            
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
    
    
    // ---------------------------------------
    // chore tooltips for building errands tab
    // ---------------------------------------
    
    // rewrites the chore tooltips for the adjusted priority system
    [HarmonyPatch(typeof(BuildingChoresPanelDupeRow))]
    [HarmonyPatch("TooltipForDupe")]
    public class ErrandsTabTooltipsOverride
    {
        public static bool Prefix(
            Chore.Precondition.Context context,
            ChoreConsumer choreConsumer,
            int rank,
            ref string __result
        ) {
            // only need to override if we're showing the calculation
            if (!context.IsPotentialSuccess()) { return true; }
            __result = Tooltips.ByImportance(context, choreConsumer);
            __result = __result.Replace("{Description}", (context.chore.driver == choreConsumer.choreDriver) ? UI.DETAILTABS.BUILDING_CHORES.DUPE_TOOLTIP_DESC_ACTIVE : UI.DETAILTABS.BUILDING_CHORES.DUPE_TOOLTIP_DESC_INACTIVE);
            // these fields are sometimes added by the description:
            __result = __result.Replace("{Name}", choreConsumer.name);
            __result = __result.Replace("{Errand}", GameUtil.GetChoreName(context.chore, context.data));
            __result = __result.Replace("{Rank}", rank.ToString());
            
            // maybe tack on a bunch of debug info
            if (Util.debugTooltips) {
                __result += "\n\n" + ChoreTooltipsOverride.DebugInfo(context, choreConsumer);
            }
            return false;
        }
    }
    
    
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
                Util.TaskImportance(out_context));
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
