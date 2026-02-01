using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

using Meta.XR.ImmersiveDebugger;

namespace MultiSet.Samples.OVRFix
{
    /**
     * Optimized version of SelectList with search debouncing and item reuse.
     */
    public class OptimizedSelectList : MonoBehaviour
    {
        // to render stuff
        public RectTransform content;      // parent of spawn point
        public Transform SpawnPoint;       // spawn point of items
        public GameObject spawnItem;       // prefab of item to be spawned
        public int heightOfPrefab;         // height of spawnItem

        // additional UI
        public TMP_InputField searchField;
        public GameObject resetButtonSearchField;
        public GameObject placeholder;

        public List<ListItemData> pois; // all items available for list
        private List<ListItemData> currentItemsTotal;
        
        [Header("Optimization Settings")]
        public float searchDebounceTime = 0.3f;
        
        [Header("Debug Info (Meta Immersive Debugger)")]
        [DebugMember] private int _interactionCount = 0;
        [DebugMember] private string _lastInteractionType = "None";
        [DebugMember] private bool _isCurrentlyFocused = false;
        [DebugMember] private string _keyboardStatus = "Hidden";
        [DebugMember] private bool _isKeyboardSupported = false;
        [DebugMember] private string _keyboardObjectStatus = "None";

        [DebugMember]
        public void ManualKeyboardTrigger()
        {
            Debug.Log("[OptimizedSelectList] ManualKeyboardTrigger button pressed.");
            OpenKeyboard();
        }

        private List<GameObject> _pooledItems = new List<GameObject>();
        private Coroutine _searchCoroutine;
        private Coroutine _keyboardCoroutine;

        public void Awake()
        {
            PrepareAllData();
            _isKeyboardSupported = TouchScreenKeyboard.isSupported;
            
            if (searchField != null)
            {
                Debug.Log("[OptimizedSelectList] Initializing searchField listeners.");
                
                // Set to true to allow interaction with Unity canvas while keyboard is open.
                // This prevents the OS from showing its own input field overlay which steals controller focus.
                TouchScreenKeyboard.hideInput = true;

                // 1. Basic selection listener
                searchField.onSelect.AddListener((s) => {
                    Debug.Log("[OptimizedSelectList] onSelect fired.");
                    OpenKeyboard();
                });

                // 2. Add EventTrigger for robust VR interaction
                var eventTrigger = searchField.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (eventTrigger == null) eventTrigger = searchField.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                
                AddEventTriggerEntry(eventTrigger, UnityEngine.EventSystems.EventTriggerType.PointerClick);
                AddEventTriggerEntry(eventTrigger, UnityEngine.EventSystems.EventTriggerType.PointerDown);
                
                // 3. Monitor focus state for debugger
                StartCoroutine(MonitorFocus());
            }
        }

        private void AddEventTriggerEntry(UnityEngine.EventSystems.EventTrigger trigger, UnityEngine.EventSystems.EventTriggerType type)
        {
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
            entry.eventID = type;
            entry.callback.AddListener((data) => { 
                _interactionCount++;
                _lastInteractionType = type.ToString();
                Debug.Log($"[OptimizedSelectList] {type} fired via EventTrigger.");
                OpenKeyboard(); 
            });
            trigger.triggers.Add(entry);
        }

        private IEnumerator MonitorFocus()
        {
            while (true)
            {
                _isCurrentlyFocused = searchField != null && searchField.isFocused;
                _keyboardStatus = (TouchScreenKeyboard.visible) ? "Visible" : "Hidden";
                yield return new WaitForSeconds(0.5f);
            }
        }

        public void OpenKeyboard()
        {
            if (searchField == null) return;
            Debug.Log("[OptimizedSelectList] OpenKeyboard called.");
            
            // Decouple: Stop any previous attempt to focus/open to avoid conflicts
            if (_keyboardCoroutine != null) StopCoroutine(_keyboardCoroutine);
            _keyboardCoroutine = StartCoroutine(ForceFocusAndOpenKeyboard());
        }

        private IEnumerator ForceFocusAndOpenKeyboard()
        {
            Debug.Log("[OptimizedSelectList] Starting Robust ForceFocusAndOpenKeyboard.");
            
            // 1. Ensure EventSystem selection
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(searchField.gameObject);
            }
            
            // 2. Clear focus first to trigger a fresh select event
            searchField.DeactivateInputField();
            yield return null;

            // 3. Request focus with UI updates
            Canvas.ForceUpdateCanvases();
            searchField.Select();
            searchField.ActivateInputField();
            
            // 4. Aggressive Focus Loop
            for (int i = 0; i < 10; i++)
            {
                if (searchField.isFocused) break;
                
                searchField.ActivateInputField();
                _isCurrentlyFocused = searchField.isFocused;
                Debug.Log($"[OptimizedSelectList] Focus retry {i+1}. Focused: {_isCurrentlyFocused}");
                yield return new WaitForSeconds(0.05f); 
            }

            _isCurrentlyFocused = searchField.isFocused;

            // 5. Open Keyboard with Retry
            if (_isCurrentlyFocused)
            {
                Debug.Log("[OptimizedSelectList] Calling TouchScreenKeyboard.Open.");
                
                // Open keyboard
                var kb = TouchScreenKeyboard.Open(searchField.text, TouchScreenKeyboardType.Default, true, false, false, false, "Search...");
                _keyboardObjectStatus = (kb != null) ? "Created" : "Null/Failed";
                
                // Wait for it to show up on the system level
                float timer = 0f;
                while (timer < 0.5f)
                {
                    if (TouchScreenKeyboard.visible || (kb != null && kb.active))
                    {
                        Debug.Log("[OptimizedSelectList] Keyboard is now visible/active.");
                        yield break;
                    }
                    timer += 0.1f;
                    yield return new WaitForSeconds(0.1f);
                }

                // If still not showing, kick it one more time
                Debug.LogWarning("[OptimizedSelectList] Keyboard timeout, retrying Open once more.");
                TouchScreenKeyboard.Open(searchField.text, TouchScreenKeyboardType.Default, true, false, false, false, "Search...");
            }
            else
            {
                Debug.LogError("[OptimizedSelectList] Failed to acquire focus even after retry loop.");
                _keyboardObjectStatus = "Failed (No Focus)";
            }
        }

        void PrepareAllData()
        {
            pois = new List<ListItemData>();

            foreach (var poi in NavigationController.instance.augmentedSpace.GetPOIs())
            {
                pois.Add(poi);
            }
        }

        public void RenderPOIs()
        {
            currentItemsTotal = pois;
            RenderList(pois);
        }

        /**
         * Renders given items as a list using object pooling
         */
        public void RenderList(List<ListItemData> items)
        {
            // sort pois alphabetically
            items.Sort(CompareItemTitle);

            int poisCount = items.Count;

            // Ensure we have enough pooled items
            while (_pooledItems.Count < poisCount)
            {
                GameObject newItem = Instantiate(spawnItem, SpawnPoint);
                newItem.SetActive(false);
                _pooledItems.Add(newItem);
            }

            // Update items and position them
            for (int i = 0; i < _pooledItems.Count; i++)
            {
                if (i < poisCount)
                {
                    ListItemData item = items[i];
                    float spawnY = (i * heightOfPrefab);
                    _pooledItems[i].transform.localPosition = new Vector3(SpawnPoint.localPosition.x, -spawnY, SpawnPoint.localPosition.z);
                    
                    _pooledItems[i].SetActive(true);
                    
                    // Try to get the optimized component first, fall back to standard if not replaced yet
                    var itemUI = _pooledItems[i].GetComponent<OptimizedListItemUI>();
                    if (itemUI != null)
                    {
                        itemUI.SetListItemData(item);
                    }
                    else
                    {
                        var standardUI = _pooledItems[i].GetComponent<ListItemUI>();
                        if (standardUI != null) standardUI.SetListItemData(item);
                    }
                }
                else
                {
                    _pooledItems[i].SetActive(false);
                }
            }

            // set content holder height
            content.sizeDelta = new Vector2(0, poisCount * heightOfPrefab);
        }

        public void ResetPOISearch()
        {
            if (_searchCoroutine != null) StopCoroutine(_searchCoroutine);
            searchField.text = "";
            
            // Clear focus to ensure the keyboard closes when resetting
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            resetButtonSearchField.SetActive(false);
            RenderList(currentItemsTotal);
            if (placeholder != null) placeholder.SetActive(true);
        }

        public void SelectSearchInputField()
        {
            searchField.Select();
        }

        /**
         * Debounced search call.
         */
        public void SearchPOIOnSearchChanged(string search)
        {
            if (_searchCoroutine != null) StopCoroutine(_searchCoroutine);
            _searchCoroutine = StartCoroutine(DebouncedSearch(search));
        }

        private IEnumerator DebouncedSearch(string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                resetButtonSearchField.SetActive(false);
            }
            else
            {
                resetButtonSearchField.SetActive(true);
                yield return new WaitForSeconds(searchDebounceTime);
            }

            RenderList(FilterByTitle(search));
        }

        List<ListItemData> FilterByTitle(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm)) return currentItemsTotal;
            
            string search = searchTerm.ToLower();
            return currentItemsTotal.FindAll(x => x.listTitle.ToLower().Contains(search));
        }

        public void ResetSearch()
        {
            if (_searchCoroutine != null) StopCoroutine(_searchCoroutine);
            searchField.text = "";

            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            if (placeholder != null) placeholder.SetActive(true);
        }

        int CompareItemTitle(ListItemData a, ListItemData b)
        {
            var part1 = a.listTitle;
            var part2 = b.listTitle;
            var compareResult = part1.CompareTo(part2);
            if (compareResult == 0)
            {
                return b.listTitle.CompareTo(a.listTitle);
            }
            return compareResult;
        }
    }
}
