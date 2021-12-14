using HarmonyLib; // Harmony
using KMod; // UserMod2
using System.Collections.Generic; // List
using STRINGS;
using UnityEngine; // GameObject

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
    
    public class Options
    {
        // whether we should include the strength value at all.
        // currently disabled because it has no stock translations.
        public static bool UseStrength = false;
    }
    
    public class Globals
    {
        // these need to be set by one patch, and read from another.
        public static bool DisplayRAF = false;
        public static bool DisplayStrength = false;
    }
    
    
    // -------------------------------------------------------
    // add full material properties to material selection info
    // -------------------------------------------------------
    // ref: AdditionalDetailsPanel, Element.FullDescription()
    // used in: MaterialSelector.SetEffects
    //              (material selection panel description)
    //          MaterialSelector.UpdateMaterialTooltips
    //              (material selection panel hover tooltips)
    //          SelectedRecipeQueueScreen.GetResultDescriptions
    //              (fabrication result tooltips)
    
    [HarmonyPatch(typeof(MaterialSelector))]
    [HarmonyPatch("SetEffects")]
    public class EnableTileMaterialInfo
    {
        public static void Prefix(
            Recipe ___activeRecipe)
        {
            // this is a bit of a mess.
            // for stock buildings it blocks radiation if any of these apply:
            // * it is a SimCellOccupier and doReplaceElement is true
            //   (example: tiles)
            // * it has a MakeBaseSolid.Def def with occupyFoundationLayer true
            //   (example: fish feeder)
            // * it's not a SimCellOccupier, but is marked IsFoundation
            //   (example: doors)
            // of these, i'm least confident about the third,
            // so for now i'll only apply it if it's a door.
            BuildingDef def = ___activeRecipe.GetBuildingDef();
            GameObject building = def.BuildingComplete;
            //Tag result = ___activeRecipe.Result;
            //Debug.LogFormat("MS.SE: result.Name: {0}", result.Name);
            
            bool rad = false;
            
            // a SimCellOccupier with doReplaceElement blocks radiation
            SimCellOccupier sco = building.GetComponent<SimCellOccupier>();
            if (sco != null && sco.doReplaceElement) { rad = true; }
            
            // a MakeBaseSolid.Def with occupyFoundationLayer blocks radiation
            MakeBaseSolid.Def mbs = building.GetDef<MakeBaseSolid.Def>();
            if (mbs != null && mbs.occupyFoundationLayer) { rad = true; }
            
            // a Door with IsFoundation (on the building def) blocks radiation
            Door door = building.GetComponent<Door>();
            if (door != null && def.IsFoundation) { rad = true; }
            
            // enable radiation factor display if appropriate
            if (rad)
            {
                Globals.DisplayRAF = true;
                if (Options.UseStrength) { Globals.DisplayStrength = true; }
            }
        }
        
        public static void Postfix()
        {
            // just reset these, even if we didn't change anything.
            Globals.DisplayRAF = false;
            Globals.DisplayStrength = false;
        }
    }
    
    
    [HarmonyPatch(typeof(GameUtil))]
    [HarmonyPatch("GetSignificantMaterialPropertyDescriptors")]
    public class MaterialPropertyInformationPatch
    {
        // note: these are only used if Options.UseStrength is true.
        // otherwise this mod remains translation-agnostic.
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
            
            // radiation absorbtion factor (and emission, if relevant)
            // -------------------------------------------------------
            // DLC only.
            // Globals.DisplayRAF will be set to true if appropriate.
            // this is done by EnableTileMaterialInfo above.
            if (DlcManager.FeatureRadiationEnabled() && Globals.DisplayRAF)
            {
                // absorbtion factor and emission are annoyingly conflated.
                // ELEMENTS.RADIATIONPROPERTIES ==
                //    "Radiation Absorbtion Factor: {0}\n" +
                //    "Radiation Emission/1000kg: {1}"
                string[] radprops_split = ELEMENTS.RADIATIONPROPERTIES.text.Split('\n');
                
                Descriptor RAF = default(Descriptor);
                // buildings absorb a fixed percentage of radiation.
                string formattedPercent = GameUtil.GetFormattedPercent(
                    // note: mass is not used for constructed objects.
                    // in fact there is only a flat multiplier of 0.8
                    // to the radiation absorbtion factor of the material.
                    GameUtil.GetRadiationAbsorptionPercentage(
                        element, mass:0f, isConstructed:true
                    ) * 100f
                );
                RAF.SetupDescriptor(
                    string.Format(
                        UI.DETAILTABS.DETAILS.RADIATIONABSORPTIONFACTOR.NAME,
                        formattedPercent),
                    string.Format(
                        UI.DETAILTABS.DETAILS.RADIATIONABSORPTIONFACTOR.TOOLTIP,
                        formattedPercent)
                );
                
                // we could display the radiation absorbtion factor.
                // it doesn't actually have its own string,
                // so we have to split it from a combined one,
                // and there's no tooltip.
                //string radprops = ELEMENTS.RADIATIONPROPERTIES.text.Split('\n');
                //string radprops_absorbtion = radprops_split[0];
                //RAF.SetupDescriptor(
                //    string.Format(
                //        radprops_absorbtion,
                //        element.radiationAbsorptionFactor),
                //    "" // no relevant tooltip currently exists
                //);
                
                // we could also display both RAF and emission together,
                // as that's what the actual in-game string does.
                // but emission is really only very seldom non-zero
                // so it's completely irrelevant in most cases.
                //RAF.SetupDescriptor(
                //    string.Format(
                //        ELEMENTS.RADIATIONPROPERTIES,
                //        element.radiationAbsorptionFactor,
                //        GameUtil.GetFormattedRads(
                //            element.radiationPer1000Mass * 1.1f / 600f,
                //            GameUtil.TimeSlice.PerCycle)),
                //    "" // no relevant tooltip currently exists
                //);
                
                RAF.IncreaseIndent();
                __result.Add(RAF);
                
                // radiation emission
                // ------------------
                // items that absorb radiation are also those that can emit.
                // only displayed if nonzero.
                if (element.radiationPer1000Mass > 0
                    && radprops_split.Length > 1)
                {
                    string radprops_emission = radprops_split[1].Replace("{1}","{0}");
                    Descriptor RE = default(Descriptor);
                    RE.SetupDescriptor(
                        string.Format(
                            radprops_emission,
                            GameUtil.GetFormattedRads(
                                element.radiationPer1000Mass * 1.1f / 600f,
                                GameUtil.TimeSlice.PerCycle)),
                        "" // no relevant tooltip currently exists
                    );
                    RE.IncreaseIndent();
                    __result.Add(RE);
                }
            }
            
            
            // material strength
            // -----------------
            // this is a hidden property!
            // pressure damage depends on this, not hardness.
            // some info is on the wiki under "Liquid".
            // it's currently disabled, as it has no pre-translated string.
            if (Globals.DisplayStrength)
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
