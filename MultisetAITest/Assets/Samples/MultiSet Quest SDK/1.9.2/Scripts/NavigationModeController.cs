using UnityEngine;
using UnityEngine.UI;

namespace MultiSet.Samples
{
    /// <summary>
    /// Manages the switch between Human Navigation (Arrow) and Robot Navigation.
    /// </summary>
    public class NavigationModeController : MonoBehaviour
    {
        public enum NavigationMode
        {
            Human,
            Robot
        }

        [Header("Controllers")]
        public RobotNavigator robotNavigator;
        public NavigationController navigationController;
        public ShowPath showPath; // The arrow visualization

        [Header("UI")]
        public Toggle modeToggle; // Optional: Reference to a UI toggle if available

        private NavigationMode _currentMode = NavigationMode.Human;

        void Start()
        {
            if (robotNavigator == null) robotNavigator = FindObjectOfType<RobotNavigator>();
            if (navigationController == null) navigationController = FindObjectOfType<NavigationController>();
            if (showPath == null) showPath = FindObjectOfType<ShowPath>();

            // Initialize default state
            SetMode(NavigationMode.Robot);

            // Hook up toggle if assigned
            if (modeToggle != null)
            {
                modeToggle.onValueChanged.AddListener(OnToggleChanged);
            }
        }

        public void OnToggleChanged(bool isRobotMode)
        {
            SetMode(isRobotMode ? NavigationMode.Robot : NavigationMode.Human);
        }

        public void SetMode(NavigationMode mode)
        {
            _currentMode = mode;
            Debug.Log($"Navigation Mode set to: {mode}");

            if (mode == NavigationMode.Human)
            {
                // Enable Arrow
                if (showPath != null) showPath.enabled = true;
                
                // Disable Robot
                if (robotNavigator != null)
                {
                    robotNavigator.StopRobot();
                    robotNavigator.gameObject.SetActive(false); // Hide robot or just stop? User said "robot will go...", implies it might be visible always or only in robot mode. Let's keep it active but stopped, or maybe hide it if it's "Human" mode. 
                    // Let's assume we hide it for now to be clean, or just stop it.
                    // If we hide it, we need to make sure we show it when switching back.
                    robotNavigator.gameObject.SetActive(false);
                }
            }
            else // Robot Mode
            {
                // Disable Arrow
                if (showPath != null) showPath.enabled = false;

                // Enable Robot
                if (robotNavigator != null)
                {
                    robotNavigator.gameObject.SetActive(true);
                    // If we are already navigating, start the robot immediately
                    if (navigationController != null && navigationController.IsCurrentlyNavigating())
                    {
                        robotNavigator.StartRobotNavigation();
                    }
                }
            }
        }

        /// <summary>
        /// Called when a new destination is set (e.g. by UI or POI selection).
        /// You might need to hook this up to the NavigationController's event or call it manually.
        /// </summary>
        public void OnDestinationSet()
        {
            if (_currentMode == NavigationMode.Robot && robotNavigator != null)
            {
                robotNavigator.StartRobotNavigation();
            }
        }
    }
}
