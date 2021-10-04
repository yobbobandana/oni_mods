using HarmonyLib; // Harmony
using KMod; // UserMod2
using System.Collections.Generic; // List
using System.Linq; // Last

namespace AutomaticGeyserCalculation
{
    // this just does the default thing
    public class AutomaticGeyserCalculation : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }
    
    // ------------------------------------------------------
    // add various extra information to the geyser info panel
    // ------------------------------------------------------
    // currently added:
    //   flow information:
    //     * average flow while active
    //     * buffer required to maintain flow while non-dormant ("eruption buffer")
    //     * total average flow
    //     * buffer required to maintain permanent flow ("dormancy buffer")
    //   thermal information:
    //     * cooling potential below 20°C (erupting / active / total average)
    //     * thermal output above 95°C (erupting / active / total average)
    //       + tooltip hints relating heat output to steam turbines
    //       + special tooltip hints for steam geysers
    
    [HarmonyPatch(typeof(Geyser))]
    [HarmonyPatch("GetDescriptors")]
    public class GeyserInformationPatch
    {
        // Category Header: Flow
        public static LocString CategoryFlowLabel = "<b>Flow Information:</b>";
        public static LocString CategoryFlowTooltip = "Calculated information relating to the rate of flow of this geyser";
        
        // Average Flow While Active
        public static LocString ActiveFlowLabel = "Average Active Flow: {0}";
        public static LocString ActiveFlowTooltip = "This geyser outputs {0} on average during its active (non-dormant) period";
        
        // Eruption Buffer
        public static LocString EruptionBufferLabel = "Eruption Buffer: {0}";
        public static LocString EruptionBufferTooltip = "{0} should be buffered to maintain {1} constant flow during the active period";
        
        // Total Average Flow
        public static LocString TotalFlowLabel = "Total Average Flow: {0}";
        public static LocString TotalFlowTooltip = "This geyser outputs {0} on average, taking into account eruption times and dormancy";
        
        // Total Average Flow (not yet analysed)
        public static LocString HiddenTotalFlowLabel = "Total Average Flow: (Requires Analysis)";
        public static LocString HiddenTotalFlowTooltip = "The total average output flow of this geyser, taking into account eruption times and dormancy";
        
        // Dormancy Buffer
        public static LocString DormancyBufferLabel = "Dormancy Buffer: {0}";
        public static LocString DormancyBufferTooltip = "{0} of output should be buffered to sustain a constant flow of {1} across the dormancy period";
        
        // Dormancy Buffer (not yet analysed)
        public static LocString HiddenDormancyBufferLabel = "Dormancy Buffer: (Requires Analysis)";
        public static LocString HiddenDormancyBufferTooltip = "How much output should be buffered to sustain average flow across the dormancy period";
        
        // Category Header: Heat
        public static LocString CategoryHeatLabel = "<b>Heat Production (over {0}):</b>";
        public static LocString CategoryHeatTooltip = "How much thermal energy is produced during the given phase, relative to a target temperature of {0}";
        
        // Positive Thermal Output - heating over 95°C
        public static LocString PeakHeatLabel = "Erupting: {0}";
        public static LocString PeakHeatTooltip = "{0} of thermal energy is produced while erupting";
        public static LocString ActiveHeatLabel = "Active: {0}";
        public static LocString ActiveHeatTooltip = "On average {0} of thermal energy is produced during the active (non-dormant) period";
        public static LocString TotalHeatLabel = "Total Average: {0}";
        public static LocString TotalHeatTooltip = "In total {0} of thermal energy is produced by this geyser, averaging across its entire lifetime";
        public static LocString HiddenTotalHeatLabel = "Total Average: (Requires Analysis)";
        public static LocString HiddenTotalHeatTooltip = "Total average thermal energy output, including the dormancy period";
        
        // Category Header: Cool
        public static LocString CategoryCoolLabel = "<b>Cooling Output (to {0}):</b>";
        public static LocString CategoryCoolTooltip = "How much cooling this geyser provides during the given phase, relative to a target temperature of {0}";
        
        // Negative Thermal Output - cooling below 20°C
        public static LocString PeakCoolLabel = "Erupting: {0}";
        public static LocString PeakCoolTooltip = "This geyser provides {0} of cooling while erupting";
        public static LocString ActiveCoolLabel = "Active: {0}";
        public static LocString ActiveCoolTooltip = "On average this geyser provides {0} of cooling during its active (non-dormant) period";
        public static LocString TotalCoolLabel = "Total Average: {0}";
        public static LocString TotalCoolTooltip = "In total this geyser provides {0} of cooling, averaging across its entire lifetime";
        public static LocString HiddenTotalCoolLabel = "Total Average: (Requires Analysis)";
        public static LocString HiddenTotalCoolTooltip = "Total average cooling output, including the dormancy period";
        
        // Steam Turbine Calculation
        public static LocString SteamTurbinePower = "This amount of heat energy could fully power {0:N1} steam turbines, if directed appropriately";
        public static LocString SteamTurbineRestricted = "Steam output during this period can directly feed {0:N1} steam turbines restricted to {1} open vents each";
        public static LocString SteamTurbineUnrestricted = "Steam output during this period can directly feed {0:N1} steam turbines";
        public static LocString SteamTurbineColdWarning = "The output of this geyser is cold and will require heating";
        public static LocString SteamTurbineHotWarning = "The output of this geyser is hot and some energy may be wasted";
        
        // add our descriptors in after the base descriptors
        public static void Postfix(ref Geyser __instance, ref List<Descriptor> __result)
        {
            // ----------
            // flow rates
            // ----------
            
            // category header
            __result.Add(new Descriptor(CategoryFlowLabel, CategoryFlowTooltip));
            
            // precalculate geyser data as most of it is codependant.
            // average flow while active
            float flow = __instance.configuration.GetEmitRate();
            float eruptingProportion = __instance.configuration.GetIterationPercent();
            float activeFlow = flow * eruptingProportion;
            // total average flow
            float activeProportion = __instance.configuration.GetYearPercent();
            float totalFlow = activeFlow * activeProportion;
            // emission buffer
            float idleSeconds = __instance.configuration.GetOffDuration();
            float eruptionBuffer = activeFlow * idleSeconds;
            // dormancy buffer
            float dormantSeconds = __instance.configuration.GetYearOffDuration();
            float dormancyBuffer = totalFlow * dormantSeconds;
            
            // average flow while active - doesn't need analysis
            string activeFlowStr = GameUtil.GetFormattedMass(activeFlow, GameUtil.TimeSlice.PerSecond);
            __result.Add(new Descriptor(
                string.Format(ActiveFlowLabel, activeFlowStr),
                string.Format(ActiveFlowTooltip, activeFlowStr)
            ).IncreaseIndent());
            
            // eruption buffer - doesn't need analysis
            string ebufferStr = GameUtil.GetFormattedMass(eruptionBuffer);
            __result.Add(new Descriptor(
                string.Format(EruptionBufferLabel, ebufferStr),
                string.Format(EruptionBufferTooltip, ebufferStr, activeFlowStr)
            ).IncreaseIndent());
            
            // total average output and dormancy buffer require analysis
            Studyable component = __instance.GetComponent<Studyable>();
            bool requiresAnalysis = ((bool)component && !component.Studied);
            if (requiresAnalysis)
            {
                // descriptors when not analysed
                __result.Add(new Descriptor(HiddenTotalFlowLabel, HiddenTotalFlowTooltip).IncreaseIndent());
                __result.Add(new Descriptor(HiddenDormancyBufferLabel, HiddenDormancyBufferTooltip).IncreaseIndent());
            }
            else
            {
                string totalFlowStr = GameUtil.GetFormattedMass(totalFlow, GameUtil.TimeSlice.PerSecond);
                // total average output
                __result.Add(new Descriptor(
                    string.Format(TotalFlowLabel, totalFlowStr),
                    string.Format(TotalFlowTooltip, totalFlowStr)
                ).IncreaseIndent());
                
                // dormancy buffer
                string dbufferStr = GameUtil.GetFormattedMass(dormancyBuffer);
                __result.Add(new Descriptor(
                    string.Format(DormancyBufferLabel, dbufferStr),
                    string.Format(DormancyBufferTooltip, dbufferStr, totalFlowStr)
                ).IncreaseIndent());
            }
            
            // ------------------
            // thermal properties
            // ------------------
            
            Element element = ElementLoader.FindElementByHash(__instance.configuration.GetElement());
            float SHC = element.specificHeatCapacity;
            float temperature = __instance.configuration.GetTemperature();
            const float heatThreshold = 368.15f; // 95°C
            const float coolThreshold = 293.15f; // 20°C
            string heatTempStr = GameUtil.GetFormattedTemperature(heatThreshold);
            string coolTempStr = GameUtil.GetFormattedTemperature(coolThreshold);
            
            // only display heat energy if above the heat threshold
            float heat = temperature - heatThreshold;
            if (heat > 0)
            {
                // category header
                __result.Add(new Descriptor(
                    string.Format(CategoryHeatLabel, heatTempStr),
                    string.Format(CategoryHeatTooltip, heatTempStr)
                ));
                
                // flow is in kg so multiply by 1000 to get result in grams
                float peakHeat = SHC * heat * flow * 1000;
                float activeHeat = peakHeat * eruptingProportion;
                float totalHeat = activeHeat * activeProportion;
                bool isSteam = element.id == SimHashes.Steam;
                string label;
                string tooltip;
                string heatEnergyStr;
                
                // peak thermal energy output - no analysis required
                heatEnergyStr = GameUtil.GetFormattedHeatEnergyRate(peakHeat);
                label = string.Format(PeakHeatLabel, heatEnergyStr);
                tooltip = string.Format(PeakHeatTooltip, heatEnergyStr);
                tooltip += SteamTurbineFootnote(peakHeat, temperature, flow, isSteam);
                __result.Add(new Descriptor(label, tooltip).IncreaseIndent());
                
                // average active thermal energy output - no analysis required
                heatEnergyStr = GameUtil.GetFormattedHeatEnergyRate(activeHeat);
                label = string.Format(ActiveHeatLabel, heatEnergyStr);
                tooltip = string.Format(ActiveHeatTooltip, heatEnergyStr);
                tooltip += SteamTurbineFootnote(activeHeat, temperature, activeFlow, isSteam);
                __result.Add(new Descriptor(label, tooltip).IncreaseIndent());
                
                // total average thermal energy output - requires analysis
                if (requiresAnalysis)
                {
                    __result.Add(new Descriptor(HiddenTotalHeatLabel, HiddenTotalHeatTooltip).IncreaseIndent());
                }
                else
                {
                    heatEnergyStr = GameUtil.GetFormattedHeatEnergyRate(totalHeat);
                    label = string.Format(TotalHeatLabel, heatEnergyStr);
                    tooltip = string.Format(TotalHeatTooltip, heatEnergyStr);
                    tooltip += SteamTurbineFootnote(totalHeat, temperature, totalFlow, isSteam);
                    __result.Add(new Descriptor(label, tooltip).IncreaseIndent());
                }
            }
            
            // only display cool energy if below the cool threshold
            float cool = coolThreshold - temperature;
            if (cool > 0)
            {
                // category header
                __result.Add(new Descriptor(
                    string.Format(CategoryCoolLabel, coolTempStr),
                    string.Format(CategoryCoolTooltip, coolTempStr)
                ));
                
                // flow is in kg so multiply by 1000 to get result in grams
                float peakCool = SHC * cool * flow * 1000;
                float activeCool = peakCool * eruptingProportion;
                float totalCool = activeCool * activeProportion;
                
                // peak cooling output - no analysis required
                string peakCoolStr = GameUtil.GetFormattedHeatEnergyRate(peakCool);
                __result.Add(new Descriptor(
                    string.Format(PeakCoolLabel, peakCoolStr),
                    string.Format(PeakCoolTooltip, peakCoolStr)
                ).IncreaseIndent());
                
                // average cooling output while active - no analysis required
                string activeCoolStr = GameUtil.GetFormattedHeatEnergyRate(activeCool);
                __result.Add(new Descriptor(
                    string.Format(ActiveCoolLabel, activeCoolStr),
                    string.Format(ActiveCoolTooltip, activeCoolStr)
                ).IncreaseIndent());
                
                // total average cooling output - requires analysis
                if (requiresAnalysis)
                {
                    __result.Add(new Descriptor(HiddenTotalCoolLabel, HiddenTotalCoolTooltip).IncreaseIndent());
                }
                else
                {
                    string totalCoolStr = GameUtil.GetFormattedHeatEnergyRate(totalCool);
                    __result.Add(new Descriptor(
                        string.Format(TotalCoolLabel, totalCoolStr),
                        string.Format(TotalCoolTooltip, totalCoolStr)
                    ).IncreaseIndent());
                }
            }
        }
        
        
        // helper function for calculating number of steam turbines required.
        // flow should be in kg/s, temperature in K
        public static string SteamTurbineFootnote(float heatEnergy, float temperature, float flow, bool isSteam = false)
        {
            float turbines;
            string result = "\n\n";
            const float minTemp = 398.15f; // minimum usable steam temperature
            const float maxTemp = 630.65f; // maximum usable steam temperature
            if (!isSteam)
            {
                // in this case just divide the heat energy by turbine max efficiency.
                // the player is assumed to pass along the correct amount of heat,
                // and block vents as appropriate.
                const float maxEfficiency = 877590f;
                turbines = heatEnergy / maxEfficiency;
                result += string.Format(SteamTurbinePower, turbines);
                if (temperature < minTemp)
                {
                    result += "\n\n";
                    result += SteamTurbineColdWarning;
                }
                return result;
            }
            // if it's a steam geyser we need to take into account flow rate
            float turbineFlow = 2.0f;
            int vents = 5;
            if (temperature <= 473.15f) { vents = 5; }
            else if (temperature <= 499.4f) { vents = 4; turbineFlow = 1.6f; }
            else if (temperature <= 543.15f) { vents = 3; turbineFlow = 1.2f; }
            else if (temperature <= 630.65f) { vents = 2; turbineFlow = 0.8f; }
            else { vents = 2; turbineFlow = 0.8f; } // some heat energy may be wasted
            turbines = flow / turbineFlow;
            if (vents == 5)
            {
                result += string.Format(SteamTurbineUnrestricted, turbines);
            }
            else
            {
                result += string.Format(SteamTurbineRestricted, turbines, vents);
            }
            if (temperature < minTemp)
            {
                result += "\n\n";
                result += SteamTurbineColdWarning;
            }
            if (temperature > maxTemp)
            {
                result += "\n\n";
                result += SteamTurbineHotWarning;
            }
            return result;
        }
    }
}
