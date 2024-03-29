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
                new Pair<Tag, string>(RoomConstraints.ConstraintTags.ToiletType, ROOMS.CRITERIA.TOILET.NAME),
                new Pair<Tag, string>(RoomConstraints.ConstraintTags.FlushToiletType, ROOMS.CRITERIA.FLUSH_TOILET.NAME),
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
                // only indent and add header if there are other types of item
                if (requirements.Count > 0 || roomDescriptors.Count > 0)
                {
                    if (effects.Count > 0)
                    {
                        desc.Add(new Descriptor(UI.BUILDINGEFFECTS.OPERATIONEFFECTS, UI.BUILDINGEFFECTS.TOOLTIPS.OPERATIONEFFECTS));
                    }
                    GameUtil.IndentListOfDescriptors(effects);
                }
                desc.AddRange(effects);
                desc.AddRange(roomDescriptors);
                active = true;
            }
            ___descriptionContainer.descriptors.gameObject.SetActive(active);
            ___descriptionContainer.descriptors.SetDescriptors(desc);
        }
    }
    
    // Build 525812 or so added the requirements class to the build info pane,
    // so that part of this is no longer required.
    // It's a bit ugly as it doesn't use the nice pretty colour...
    // but not so ugly that i'm going to change it.
}
