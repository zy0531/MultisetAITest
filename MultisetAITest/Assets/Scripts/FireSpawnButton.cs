using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MultiSet.Navigation
{
    /// <summary>
    /// Attached to a UI Button to trigger fire spawning in FireSimulationManager.
    /// Supports standard uGUI and robust VR interaction (Meta Quest).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class FireSpawnButton : MonoBehaviour, IPointerClickHandler
    {
        private Button button;
        private FireSimulationManager manager;

        private void Awake()
        {
            button = GetComponent<Button>();
            // Find the manager in the scene
            manager = FindFirstObjectByType<FireSimulationManager>();
            
            if (manager == null)
            {
                Debug.LogError("FireSpawnButton: Could not find FireSimulationManager in the scene!");
                return;
            }

            // Standard uGUI listener
            button.onClick.AddListener(OnButtonClick);

            // Add EventTrigger for robust VR interaction (especially for World Space canvases)
            EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = gameObject.AddComponent<EventTrigger>();
            
            AddTriggerEntry(trigger, EventTriggerType.PointerClick);
            AddTriggerEntry(trigger, EventTriggerType.PointerDown);
        }

        private void AddTriggerEntry(EventTrigger trigger, EventTriggerType type)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener((data) => OnButtonClick());
            trigger.triggers.Add(entry);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Specifically handling IPointerClickHandler for Meta/Unity interaction modules
            OnButtonClick();
        }

        private void OnButtonClick()
        {
            if (manager != null)
            {
                manager.TriggerFireSpawn();
            }
        }
        
        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClick);
            }
        }
    }
}
