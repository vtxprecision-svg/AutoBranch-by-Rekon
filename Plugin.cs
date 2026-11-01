using BepInEx;
using GorillaLocomotion;
using UnityEngine;

namespace AutoBranchMod
{
    [BepInPlugin("autobranch.standalone", "Auto Branch", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        private const string ForestTrees = "Environment Objects/LocalObjects_Prefab/Forest/Terrain/SmallTrees/Group1";

        private bool forestReady;
        private float nextForestCheck;
        private float nextErrorLog;

        private void Awake() => Movement.LoadSettings();

        private void Update()
        {
            try
            {
                // wait until the player exists
                if (GorillaTagger.Instance == null || GTPlayer.Instance == null || ControllerInputPoller.instance == null)
                    return;

                // wait until the forest trees exist (searched once a second, not every frame)
                if (!forestReady)
                {
                    if (Time.unscaledTime < nextForestCheck)
                        return;
                    nextForestCheck = Time.unscaledTime + 1f;

                    forestReady = Movement.GetObject(ForestTrees) != null;
                    if (!forestReady)
                        return;
                }
                else if (Movement.GetObject(ForestTrees) == null)
                {
                    forestReady = false; // left the forest / it was unloaded
                    return;
                }

                Movement.UpdateActivation();

                Movement.AutoBranch();
            }
            catch (System.Exception e)
            {
                // log at most once every couple of seconds so a recurring error can't flood the log
                if (Time.unscaledTime >= nextErrorLog)
                {
                    nextErrorLog = Time.unscaledTime + 2f;
                    Logger.LogError(e);
                }
            }
        }
    }
}
