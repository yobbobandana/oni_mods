using HarmonyLib; // Harmony
using Klei.AI; // AttributeInstance
using System.Reflection; // ConstructorInfo
using System;

namespace RationalPriority
{
    // --------------------------------------------------------
    // adapt priority when certain fetch errands are considered
    // --------------------------------------------------------
    
    [HarmonyPatch]
    public class StoragePriorityPatch1
    {
        
        public static ConstructorInfo TargetMethod()
        {
            return typeof(Chore.Precondition.Context).GetConstructor(
                new Type[]{typeof(Chore), typeof(ChoreConsumerState), typeof(bool), typeof(object)}
                );
        }
        
        public static void Postfix(
            ref Chore.Precondition.Context __instance,
            Chore chore,
            ChoreConsumerState consumer_state
        ) {
            // we can't reduce prio 1 tasks further, so don't bother trying
            if (__instance.masterPriority.priority_value <= 1) { return; }
            
            // only tweak chores of specific type.
            // i'm guessing here. Add or remove as required.
            if (chore.choreType != Db.Get().ChoreTypes.ResearchFetch
                && chore.choreType != Db.Get().ChoreTypes.StorageFetch
                && chore.choreType != Db.Get().ChoreTypes.RanchingFetch
            ) {
                return;
            }
            
            // chore should be a fetch chore
            FetchChore fetch = chore as FetchChore;
            if (fetch == null) { return; }
            
            // destination should be storage
            Storage storage = fetch.destination;
            if (storage == null) { return; }
            // and could be storage with a configurable maximum amount
            IUserControlledCapacity controlledStorage = storage.GetComponent<IUserControlledCapacity>();
            
            // consumer should be capable of carrying
            MinionIdentity minion = consumer_state.consumer.GetComponent<MinionIdentity>();
            if (minion == null) { return; }
            
            // only care if storage is more than half full
            float capacity = storage.capacityKg;
            if (controlledStorage != null) { capacity = controlledStorage.UserMaxCapacity; }
            float stored = storage.MassStored();
            if (2*stored < capacity) { return; }
            
            // only care if fetcher is capable of overfilling
            float toFill = capacity - stored;
            float carryAmount = minion.GetAttributes().Get(Db.Get().Attributes.CarryAmount).GetTotalValue();
            if (toFill >= carryAmount) {
                Debug.LogFormat("carry capacity not exceeded: {0} > {1}", toFill, carryAmount);
                return;
            }
            
            // reduce priority according to the fill amount
            float fillProportion = toFill / carryAmount;
            float prio = (float)__instance.masterPriority.priority_value;
            int newPrio = (int)(prio * fillProportion);
            if (newPrio < Chore.MIN_PLAYER_BASIC_PRIORITY) {
                newPrio = Chore.MIN_PLAYER_BASIC_PRIORITY;
            }
            Debug.LogFormat("reducing fetch prio from {0} to {1}", (int)prio, newPrio);
            __instance.masterPriority.priority_value = newPrio;
        }
    }
}
