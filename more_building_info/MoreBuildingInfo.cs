using HarmonyLib; // Harmony
using KMod; // UserMod2
using System.Collections.Generic; // List
using STRINGS; // CODEX, UI, etc
using UnityEngine; // GameObject

namespace MoreBuildingInfo
{
    // this just does the default thing
    public class MoreBuildingInfo : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }
    
    public static class Util
    {
        // this is ugly but i don't really want to try to make it better.
        // it's used by both patches so i pulled it out here.
        // ref CodexEntryGenerator.GenerateBuildingDescriptionContainers.
        public static List<Descriptor> GetRoomDescriptors(GameObject target, bool addHeader=false)
        {
            KPrefabID component = target.GetComponent<KPrefabID>();
            string[] colours = new string[8]
            {
                "#b03030", // industrial machinery
                "#70a038", // recreation building
                "#4080a0", // clinic
                "#206070", // wash station
                "#40a0a8", // advanced wash station
                "#785018", // toilet
                "#a09038", // flush toilet
                "#40a020" //decoration
            };
            Pair<Tag, string>[] array = new Pair<Tag, string>[8]
            {
                new Pair<Tag, string>(RoomConstraints.ConstraintTags.IndustrialMachinery, CODEX.BUILDING_TYPE.INDUSTRIAL_MACHINERY),
                new Pair<Tag, string>(RoomConstraints.ConstraintTags.RecBuilding, ROOMS.CRITERIA.REC_BUILDING.NAME),
                new Pair<Tag, string>(RoomConstraints.ConstraintTags.Clinic, ROOMS.CRITERIA.CLINIC.NAME),
                new Pair<Tag, string>(RoomConstraints.ConstraintTags.WashStation, ROOMS.CRITERIA.WASH_STATION.NAME),
                new Pair<Tag, string>(RoomConstraints.ConstraintTags.AdvancedWashStation, ROOMS.CRITERIA.ADVANCED_WASH_STATION.NAME),
                new Pair<Tag, string>(RoomConstraints.ConstraintTags.Toilet, ROOMS.CRITERIA.TOILET.NAME),
                new Pair<Tag, string>(RoomConstraints.ConstraintTags.FlushToilet, ROOMS.CRITERIA.FLUSH_TOILET.NAME),
                new Pair<Tag, string>(GameTags.Decoration, ROOMS.CRITERIA.DECORATIVE_ITEM.NAME)
            };
            List<Descriptor> ret = new List<Descriptor>();
            // if the building has none of them just return a blank list.
            bool has = false;
            for (int i = 0; i < array.Length; i++)
            {
                if (component.HasTag(array[i].first)) { has = true; break; }
            }
            if (!has) { return ret; }
            if (addHeader)
            {
                string headerText = "<b>" + CODEX.HEADERS.BUILDINGTYPE + ":</b>";
                // no tooltips because the codex entries don't have tooltips.
                // this is still all translation-friendly so far.
                ret.Add(new Descriptor(headerText, ""));
            }
            for (int i = 0; i < array.Length; i++)
            {
                if (component.HasTag(array[i].first))
                {
                    string text = string.Format("<b><color={0}>", colours[i]) + array[i].second + "</color></b>";
                    ret.Add(new Descriptor(text, "").IncreaseIndent());
                }
            }
            return ret;
        }
    }
    
    // --------------------------------------------------------
    // additional information when selecting existing buildings
    // --------------------------------------------------------
    [HarmonyPatch(typeof(SimpleInfoScreen))]
    [HarmonyPatch("SetPanels")]
    public class ExistingBuildingInformationPatch
    {
        // probably easiest to postfix the (private) description container
        public static void Postfix(GameObject target, ref DescriptionContainer ___descriptionContainer)
        {
            if (
                target.GetComponent<BuildingComplete>() == null
                && target.GetComponent<BuildingUnderConstruction>() == null
            ) {
                // then it's not a building
                return;
            }
            // it is a building, so let's overwrite the descriptors.
            // yes doing this over again is duplicating effort,
            // but these are not simply editable.
            List<Descriptor> desc = new List<Descriptor>();
            List<Descriptor> requirements = GameUtil.GetGameObjectRequirements(target);
            bool active = false;
            if (requirements.Count > 0)
            {
                desc.Add(new Descriptor(UI.BUILDINGEFFECTS.OPERATIONREQUIREMENTS, UI.BUILDINGEFFECTS.TOOLTIPS.OPERATIONREQUIREMENTS));
                GameUtil.IndentListOfDescriptors(requirements);
                desc.AddRange(requirements);
                active = true;
            }
            List<Descriptor> effects = GameUtil.GetGameObjectEffects(target, simpleInfoScreen: true);
            List<Descriptor> roomDescriptors = Util.GetRoomDescriptors(target, addHeader: true);
            if (effects.Count > 0 || roomDescriptors.Count > 0)
            {
                desc.Add(new Descriptor(UI.BUILDINGEFFECTS.OPERATIONEFFECTS, UI.BUILDINGEFFECTS.TOOLTIPS.OPERATIONEFFECTS));
                GameUtil.IndentListOfDescriptors(effects);
                desc.AddRange(effects);
                desc.AddRange(roomDescriptors);
                active = true;
            }
            ___descriptionContainer.descriptors.gameObject.SetActive(active);
            ___descriptionContainer.descriptors.SetDescriptors(desc);
        }
    }
    
    // -----------------------------------------------
    // additional information when planning / building
    // -----------------------------------------------
    [HarmonyPatch(typeof(ProductInfoScreen))]
    [HarmonyPatch("SetEffects")]
    public class PlanningBuildingInformationPatch
    {
        // execute after because we have to overwrite only part of it
        public static void Postfix(BuildingDef def, ref ProductInfoScreen __instance)
        {
            // the product info screen has certain hardcoded panes,
            // which are individually toggled situationally.
            // i want to tack the room requirements class onto the effects pane,
            // which has to be done by completely rewriting it;
            // because while HasDescriptors and SetDescriptors are implemented,
            // GetDescriptors is not. [unimpressed]
            List<Descriptor> allDescriptors = GameUtil.GetAllDescriptors(def.BuildingComplete);
            // note: GetEffectDescriptors indents them automatically
            List<Descriptor> effectDescriptors = GameUtil.GetEffectDescriptors(allDescriptors);
            bool active = false;
            if (effectDescriptors.Count > 0)
            {
                Descriptor item2 = default(Descriptor);
                item2.SetupDescriptor(UI.BUILDINGEFFECTS.OPERATIONEFFECTS, UI.BUILDINGEFFECTS.TOOLTIPS.OPERATIONEFFECTS);
                effectDescriptors.Insert(0, item2);
                active = true;
            }
            List<Descriptor> roomDescriptors = Util.GetRoomDescriptors(def.BuildingComplete);
            if (roomDescriptors.Count > 0)
            {
                active = true;
            }
            
            effectDescriptors.AddRange(roomDescriptors);
            __instance.ProductEffectsPane.gameObject.SetActive(value: active);
            __instance.ProductEffectsPane.SetDescriptors(effectDescriptors);
        }
    }
}
