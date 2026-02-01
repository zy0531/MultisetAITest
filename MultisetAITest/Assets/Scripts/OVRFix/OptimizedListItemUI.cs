using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

namespace MultiSet.Samples.OVRFix
{
    /**
     * Optimized version of ListItemUI that throttles distance updates.
     */
    public class OptimizedListItemUI : MonoBehaviour
    {
        [Tooltip("Title of list element in UI")]
        public TextMeshProUGUI title;

        [Tooltip("Button to start navigation")]
        public GameObject startNavigationButton;

        [Tooltip("Data object for this list item")]
        public ListItemData dataObject;

        [Tooltip("Label to show distance to the object")]
        public TextMeshProUGUI distance;

        [Header("Optimization Settings")]
        [Tooltip("How often to update distance in seconds")]
        public float minUpdateInterval = 0.5f;
        public float maxUpdateInterval = 1.5f;

        private WaitForSeconds _updateWait;
        private Coroutine _updateCoroutine;

        // Set variables for this list item. Should be called during rendering of item list.
        public void SetListItemData(ListItemData data)
        {
            dataObject = data;
            title.text = data.listTitle;

            // only enable go button if data object is poi
            startNavigationButton.SetActive(data is POI);

            // Trigger an immediate update if needed, or wait for coroutine
            UpdateDistanceText();
        }

        void OnEnable()
        {
            // Start the update loop with a random delay to prevent all 180 POIs from updating at once
            float initialDelay = Random.Range(0f, maxUpdateInterval);
            _updateCoroutine = StartCoroutine(UpdateDistanceLoop(initialDelay));
        }

        void OnDisable()
        {
            if (_updateCoroutine != null)
            {
                StopCoroutine(_updateCoroutine);
                _updateCoroutine = null;
            }
        }

        private IEnumerator UpdateDistanceLoop(float initialDelay)
        {
            yield return new WaitForSeconds(initialDelay);
            
            while (true)
            {
                UpdateDistanceText();
                yield return new WaitForSeconds(Random.Range(minUpdateInterval, maxUpdateInterval));
            }
        }

        private void UpdateDistanceText()
        {
            if (distance != null)
            {
                distance.text = GetDistance();
            }
        }

        // click on go button
        public void Go()
        {
            if (dataObject is POI)
            {
                NavigationUIController.instance.ClickedStartNavigation((dataObject as POI));
            }
        }

        // get estimated distance for this object
        string GetDistance()
        {
            if (dataObject == null || !(dataObject is POI)) return "";

            float dist = PathEstimationUtils.instance.EstimateDistanceToPosition(dataObject as POI);
            if (dist > 0)
            {
                return (int)dist + " m";
            }
            else if (dist == -2)
            {
                return "Unreachable";
            }
            else
            {
                return "-"; // Default or error state
            }
        }
    }
}
