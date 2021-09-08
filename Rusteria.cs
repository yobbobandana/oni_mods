using HarmonyLib;
using KMod;
using System.Collections.Generic;
using STRINGS;

namespace Rusteria
{
    // this just does the default thing
    public class Rusteria : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }

    // TODO: actually patch the correct thing, whatever that is
    [HarmonyPatch(typeof(Db), "Initialize")]
    public class RusteriaPatch
    {

        // Rusteria Cluster
        public static LocString CLUSTER_NAME = "Pure Rust Cluster";
        public static LocString CLUSTER_DESC = "A chilly start with fewer nearby resources.";
        // Mini Rusteria Cluster
        public static LocString MINI_CLUSTER_NAME = "Pure Rust Cluster Mini";
        public static LocString MINI_CLUSTER_DESC = "A tiny rusty starting world, with no large planetoids but many small ones.";
        // Rusteria itself
        public static LocString RUSTERIA_NAME = "Rust Moonlet";
        public static LocString RUSTERIA_DESC = "A small, cold world composed mostly of rust.\n\nRust worlds require brisk planning and careful temperature management to sustain life.";
        // Rusteria Mini
        public static LocString RUSTERIA_MINI_NAME = "Mini Rust Moonlet";
        public static LocString RUSTERIA_MINI_DESC = "A tiny, cold ball of rust and uranium.\n\nRust worlds require brisk planning and careful temperature management to sustain life.";
        // Pure Sandstone Moonlet
        public static LocString PURESANDSTONEMOONLET_NAME = "Sandstone Asteroid";
        public static LocString PURESANDSTONEMOONLET_DESC = "A small habitable world.\n\nSandstone Moonlets contain several resources conducive to healthy, happy duplicant life.";
        // Pure Wasteland Moonlet
        public static LocString PUREWASTELANDMOONLET_NAME = "Wasteland Asteroid";
        public static LocString PUREWASTELANDMOONLET_DESC = "Tiny, sandy, dry.\n\nWasteland Moonlets mostly consist of sand and sulfur, with minimal to no water. The native wildlife may prove beneficial, if any visiting duplicants survive.";
        // Swampy Marsh Moonlet
        public static LocString SWAMPYMARSHMOONLET_NAME = "Swampy Asteroid";
        //public static LocString SWAMPYMARSHMOONLET_DESC = "";
        // Rocket Expansion: Ocean/Oil
        public static LocString PUREROCKET_OCEANOIL_NAME = "Oily Oceanic Asteroid";
        public static LocString PUREROCKET_OCEANOIL_DESC = "A medium world.\n\nOily Oceanic Asteroids contain crucial resources for large-scale rocketry and high-temperature industry.";
        // Warp Expansion: Forest/Jungle
        public static LocString PUREWARP_FORESTJUNGLE_NAME = "Caustic Forest Asteroid";
        public static LocString PUREWARP_FORESTJUNGLE_DESC = "A verdant and vivacious medium-sized world.\n\nCaustic Forest asteroids have a number of useful plant and animal lifeforms, as well as a variety of biologically-useful resources.";

        public static void Prefix()
        {
            // Names and descriptions must be compiled in.

            // Rusteria Cluster
            Strings.Add($"STRINGS.CLUSTER_NAMES.RUSTERIA.NAME", CLUSTER_NAME);
            Strings.Add($"STRINGS.CLUSTER_NAMES.RUSTERIA.DESCRIPTION", CLUSTER_DESC);

            // Mini Rusteria Cluster
            Strings.Add($"STRINGS.CLUSTER_NAMES.RUSTERIA_MINI.NAME", MINI_CLUSTER_NAME);
            Strings.Add($"STRINGS.CLUSTER_NAMES.RUSTERIA_MINI.DESCRIPTION", MINI_CLUSTER_DESC);

            // Rusteria itself
            Strings.Add($"STRINGS.WORLDS.RUSTERIA.NAME", RUSTERIA_NAME);
            Strings.Add($"STRINGS.WORLDS.RUSTERIA.DESCRIPTION", RUSTERIA_DESC);

            // Rusteria mini
            Strings.Add($"STRINGS.WORLDS.RUSTERIA_MINI.NAME", RUSTERIA_MINI_NAME);
            Strings.Add($"STRINGS.WORLDS.RUSTERIA_MINI.DESCRIPTION", RUSTERIA_MINI_DESC);

            // Pure Sandstone Moonlet
            Strings.Add($"STRINGS.WORLDS.PURESANDSTONEMOONLET.NAME", PURESANDSTONEMOONLET_NAME);
            Strings.Add($"STRINGS.WORLDS.PURESANDSTONEMOONLET.DESCRIPTION", PURESANDSTONEMOONLET_DESC);

            // Pure Wasteland Moonlet
            Strings.Add($"STRINGS.WORLDS.PUREWASTELANDMOONLET.NAME", PUREWASTELANDMOONLET_NAME);
            Strings.Add($"STRINGS.WORLDS.PUREWASTELANDMOONLET.DESCRIPTION", PUREWASTELANDMOONLET_DESC);

            // Swampy Marsh Moonlet
            Strings.Add($"STRINGS.WORLDS.SWAMPYMARSHMOONLET.NAME", SWAMPYMARSHMOONLET_NAME);
            //Strings.Add($"STRINGS.WORLDS.SWAMPYMARSHMOONLET.DESCRIPTION", SWAMPYMARSHMOONLET_DESC);

            // Rocket Expansion: Ocean/Oil
            Strings.Add($"STRINGS.WORLDS.PUREROCKET_OCEANOIL.NAME", PUREROCKET_OCEANOIL_NAME);
            Strings.Add($"STRINGS.WORLDS.PUREROCKET_OCEANOIL.DESCRIPTION", PUREROCKET_OCEANOIL_DESC);

            // Warp Expansion: Forest/Jungle
            Strings.Add($"STRINGS.WORLDS.PUREWARP_FORESTJUNGLE.NAME", PUREWARP_FORESTJUNGLE_NAME);
            Strings.Add($"STRINGS.WORLDS.PUREWARP_FORESTJUNGLE.DESCRIPTION", PUREWARP_FORESTJUNGLE_DESC);

        }
    }

    // code adapted from https://github.com/daviscook477/ONI-Mods/tree/master/src/EthanolGeyser
    [HarmonyPatch(typeof(GeyserGenericConfig))]
    [HarmonyPatch("GenerateConfigs")]
    public class GeyserGenericConfig_GenerateConfigs_Patch
    {
        // liquid ethanol
        public const string EthId = "liquid_ethanol";
        public static string EthName = UI.FormatAsLink("Ethanol Geyser", $"GeyserGeneric_{EthId.ToUpper()}");
        public static string EthDescription = $"A highly pressurized geyser that periodically erupts with {UI.FormatAsLink("Liquid Ethanol", "ETHANOL")}.";

        // liquid chlorine
        public const string ChlId = "liquid_chlorine";
        public static string ChlName = UI.FormatAsLink("Liquid Chlorine Geyser", $"GeyserGeneric_{ChlId.ToUpper()}");
        public static string ChlDescription = $"A highly pressurized geyser that periodically erupts with {UI.FormatAsLink("Liquid Chlorine", "CHLORINE")}.";


        private static void Postfix(List<GeyserGenericConfig.GeyserPrefabParams> __result)
        {
            Strings.Add($"STRINGS.CREATURES.SPECIES.GEYSER.{EthId.ToUpper()}.NAME", EthName);
            Strings.Add($"STRINGS.CREATURES.SPECIES.GEYSER.{EthId.ToUpper()}.DESC", EthDescription);
            Strings.Add($"STRINGS.CREATURES.SPECIES.GEYSER.{ChlId.ToUpper()}.NAME", ChlName);
            Strings.Add($"STRINGS.CREATURES.SPECIES.GEYSER.{ChlId.ToUpper()}.DESC", ChlDescription);

            // ethanol geyser uses cool p-water animations for now
            __result.Add(new GeyserGenericConfig.GeyserPrefabParams("geyser_liquid_water_slush_kanim", 4, 2, new GeyserConfigurator.GeyserType(EthId, SimHashes.Ethanol, 263.15f, 1000f, 2000f, 500f, 60f, 1140f, 0.1f, 0.9f, 15000f, 135000f, 0.4f, 0.8f)));
            // chlorine geyser uses infectious p-water animations for now
            __result.Add(new GeyserGenericConfig.GeyserPrefabParams("geyser_liquid_water_filthy_kanim", 4, 2, new GeyserConfigurator.GeyserType(ChlId, SimHashes.Chlorine, 203.15f, 100f, 200f, 500f, 60f, 1140f, 0.1f, 0.9f, 15000f, 135000f, 0.4f, 0.8f)));
        }
    }
}
