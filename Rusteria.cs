using HarmonyLib;
using KMod;

namespace Rusteria
{
    [HarmonyPatch(typeof(Db), "Initialize")]
    public class RusteriaPatch
    {

        // Rusteria Cluster
        public static LocString CLUSTER_NAME = "Rusteria";
        public static LocString CLUSTER_DESC = "A chilly start with fewer nearby resources.";
        // Rusteria itself
        public static LocString RUSTERIA_NAME = "Rust Moonlet";
        public static LocString RUSTERIA_DESC = "A small, cold world composed mostly of rust.\n\nRust worlds require brisk planning and careful temperature management to maintain adequate food production.";
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

            // Rusteria itself
            Strings.Add($"STRINGS.WORLDS.RUSTERIA.NAME", RUSTERIA_NAME);
            Strings.Add($"STRINGS.WORLDS.RUSTERIA.DESCRIPTION", RUSTERIA_DESC);

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
    public class Rusteria : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            //PUtil.InitLibrary();
            //new PLocalization().Register();
        }
    }
}
