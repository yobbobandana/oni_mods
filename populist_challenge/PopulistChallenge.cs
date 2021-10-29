using HarmonyLib; // Harmony
using KMod; // UserMod2
using System.Reflection; // GetField

namespace PopulistChallenge
{
    // this just does the default thing
    public class PopulistChallenge : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }
    
    
    // ------------------------------------------------------
    // there is to be no escaping the selection of printables
    // ------------------------------------------------------
    
    // hide close / reject buttons from duplicant selection screen
    [HarmonyPatch(typeof(ImmigrantScreen), "OnSpawn")]
    public class ImmigrantScreenOnSpawnPatch
    {
        public static void Postfix(
            ref ImmigrantScreen __instance,
            ref KButton ___closeButton,
            ref KButton ___rejectButton
        ) {
            // disable close and reject all buttons
            ___closeButton.gameObject.SetActive(false);
            ___rejectButton.gameObject.SetActive(false);
        }
    }
    
    // disable closing the printables screen by using Escape
    [HarmonyPatch(typeof(CharacterContainer), "OnKeyDown")]
    public class CharacterContainerOnKeyDownPatch
    {
        public static bool Prefix(
            CharacterContainer __instance,
            KButtonEvent e)
        {
            if (e.IsAction(Action.Escape))
            {
                // i'm really not sure why this needs to be called,
                // but it gets called in base so i'm calling it.
                __instance.ForceStopEditingTitle();
                return false;
            }
            return true;
        }
    }
    // care package containers also catch escape, but...
    
    
    // ---------------------------------
    // no care packages shall be allowed
    // ---------------------------------
    
    // override "care packages" game setting to temporarily disable
    [HarmonyPatch(typeof(CustomGameSettings))]
    [HarmonyPatch("GetCurrentQualitySetting")]
    [HarmonyPatch(new System.Type[] { typeof(string) })]
    public class DisableCarePackages
    {
        public static bool Prefix(
            string setting_id,
            ref Klei.CustomSettings.SettingLevel __result)
        {
            Klei.CustomSettings.ToggleSettingConfig cp =
                (Klei.CustomSettings.ToggleSettingConfig)
                Klei.CustomSettings.CustomGameSettingConfigs.CarePackages;
            if (setting_id != cp.id) { return true; }
            __result = cp.off_level;
            return false;
        }
    }
    
    
    // ----------------------------------------------------
    // auto open selection screen when printables are ready
    // ----------------------------------------------------
    
    // immediately pop the window open when dupes are available to print
    [HarmonyPatch(typeof(Telepad))]
    [HarmonyPatch("Update")]
    public class AutoOpenImmigrantScreen
    {
        public static void Postfix(ref Telepad __instance)
        {
            // the immigrant screen must already be initialized.
            // there's no public member for this so we have to use reflection.
            // keep it because we want to access this later as well.
            ImmigrantScreen immigrantScreen = (ImmigrantScreen)typeof(ImmigrantScreen).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (immigrantScreen == null) { return; }
            if (GameFlowManager.Instance == null) { return; }
            if (GameFlowManager.Instance.IsGameOver()) { return; }
            if (!__instance.GetComponent<Operational>().IsOperational) { return; }
            if (!Immigration.Instance.ImmigrantsAvailable) { return; }
            if (immigrantScreen.gameObject.activeInHierarchy) { return; }
            
            // now that we're sure it is a good idea...
            // open the printables selection window.
            ImmigrantScreen.InitializeImmigrantScreen(__instance);
            
            // base code calls this, but i'm not too certain what it does.
            // seems linked to "onUIClear" in some things.
            // it seems to clear the cursor, and clear active building commands.
            // not sure whether this is actually desirable.
            // as the window opens itself,
            // it might get annoying if it clears the active player task.
            //Game.Instance.Trigger(288942073);
        }
    }
}
