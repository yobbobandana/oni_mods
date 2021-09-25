using HarmonyLib; // Harmony
using KMod; // UserMod2
using System.Collections.Generic; // List
using STRINGS;

namespace MaterialSelectionProperties
{
    // this just does the default thing
    public class MaterialSelectionProperties : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }
    
    // -------------------------------------------------------
    // add full material properties to material selection info
    // -------------------------------------------------------
    
    [HarmonyPatch(typeof(GameUtil))]
    [HarmonyPatch("GetSignificantMaterialPropertyDescriptors")]
    public class GetSignificantMaterialPropertyDescriptors_Patch
    {
        public static LocString StrengthText = "Material Strength: {0}";
        public static LocString StrengthTip = "Materials with low strength are more easily broken by liquid pressure.";
        
        public static void Postfix(Element element, List<Descriptor> __result)
        {
            string temperatureUnitSuffix = GameUtil.GetTemperatureUnitSuffix();
            
            // thermal conductivity
            // --------------------
            float tc = element.thermalConductivity;
            string tctext = string.Format(
                UI.ELEMENTAL.THERMALCONDUCTIVITY.NAME,
                GameUtil.GetDisplayThermalConductivity(tc).ToString("0.000")
            );
            string tctip = UI.ELEMENTAL.THERMALCONDUCTIVITY.TOOLTIP;
            tctip = tctip.Replace("{THERMAL_CONDUCTIVITY}", tctext + GameUtil.GetThermalConductivitySuffix());
            tctip = tctip.Replace("{TEMPERATURE_UNIT}", temperatureUnitSuffix);
            Descriptor TC = default(Descriptor);
            TC.SetupDescriptor(tctext, tctip);
            TC.IncreaseIndent();
            __result.Add(TC);
            
            // specific heat capacity
            // ----------------------
            float shc = element.specificHeatCapacity;
            string shctext = string.Format(
                UI.ELEMENTAL.SHC.NAME,
                GameUtil.GetDisplaySHC(shc).ToString("0.000")
            );
            string shctip = UI.ELEMENTAL.SHC.TOOLTIP;
            shctip = shctip.Replace("{SPECIFIC_HEAT_CAPACITY}", shctext + GameUtil.GetSHCSuffix());
            shctip = shctip.Replace("{TEMPERATURE_UNIT}", temperatureUnitSuffix);
            Descriptor SHC = default(Descriptor);
            SHC.SetupDescriptor(shctext, shctip);
            SHC.IncreaseIndent();
            __result.Add(SHC);
            
            // melting point
            // -------------
            if (element.IsSolid)
            {
                Descriptor MP = default(Descriptor);
                MP.SetupDescriptor(
                    string.Format(UI.ELEMENTAL.MELTINGPOINT.NAME,
                        GameUtil.GetFormattedTemperature(element.highTemp)),
                    string.Format(UI.ELEMENTAL.MELTINGPOINT.TOOLTIP,
                        GameUtil.GetFormattedTemperature(element.highTemp))
                );
                MP.IncreaseIndent();
                __result.Add(MP);
            }
            // we could also handle non-solids here,
            // (see AdditionalDetailsPanel.RefreshDetails)
            // but if it's not solid how are we going to build with it anyway?
            
            // material strength
            // -----------------
            // this is a hidden property!
            // pressure damage depends on this, not hardness.
            // some info is on the wiki under "Liquid".
            // it's disabled until i figure out how to make it optional.
            bool displayStrength = false;
            if (displayStrength)
            {
                Descriptor MS = default(Descriptor);
                MS.SetupDescriptor(
                    string.Format(StrengthText, element.strength.ToString("0.0")),
                    StrengthTip
                );
                MS.IncreaseIndent();
                __result.Add(MS);
            }
        }
    }
}
