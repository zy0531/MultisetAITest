using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MultiSet.Samples.OVRFix
{
    /**
     * Dynamically scales the POI label based on its distance to the camera
     * to improve readability when it is the active navigation destination.
     */
    public class DynamicScaleSign : MonoBehaviour
    {
        private POI _parentPOI;
        private Vector3 _initialScale;
        private Transform _mainCamTransform;
        
        private CanvasGroup _canvasGroup;
        private Renderer[] _renderers;
        private bool? _lastVisibilityState = null;

        [Header("Scaling Settings")]
        [Tooltip("How much the scale increases per meter of distance.")]
        public float scaleFactor = 0.1f; 
        
        [Tooltip("The maximum scale multiplier for the label.")]
        public float maxScaleMultiplier = 5.0f;
        
        [Tooltip("The minimum scale multiplier for the label.")]
        public float minScaleMultiplier = 1.0f;

        [Header("Targeting")]
        [Tooltip("The visual object to scale/hide (usually the 'Sign' child). NEVER point this to the root POI.")]
        public GameObject visualObject;

        void Start()
        {
            if (Camera.main != null) _mainCamTransform = Camera.main.transform;

            _parentPOI = GetComponentInParent<POI>();
            
            // Try to auto-resolve to the POI's sign if not manually assigned
            if (visualObject == null && _parentPOI != null && _parentPOI.sign != null)
            {
                visualObject = _parentPOI.sign.gameObject;
            }
            
            if (visualObject == null)
            {
                visualObject = this.gameObject;
                if (_parentPOI != null && _parentPOI.gameObject == this.gameObject)
                {
                    Debug.LogWarning("[DynamicScaleSign] Script is on POI Root and visualObject is Root! This WILL break navigation. Please assign the 'Sign' child to visualObject.");
                }
            }

            // Cache components on the visual object ONLY
            _canvasGroup = visualObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = visualObject.AddComponent<CanvasGroup>();
            
            _renderers = visualObject.GetComponentsInChildren<Renderer>(true);
            
            _initialScale = visualObject.transform.localScale;
        }

        void Update()
        {
            if (_parentPOI == null || NavigationController.instance == null || _mainCamTransform == null) return;

            bool isDestination = NavigationController.instance.currentDestination == _parentPOI;
            
            // 1. Exclusive Visibility Logic
            // We use alpha and renderer toggle to keep the object active but hidden.
            if (_lastVisibilityState != isDestination)
            {
                _lastVisibilityState = isDestination;
                SetVisualsVisible(isDestination);
            }

            // 2. Scaling Logic
            if (isDestination)
            {
                // Safety: Even if visualObject is the root, we check distance to preserve NavMesh anchor.
                // But it's much better to scale the 'Sign' child specifically.
                float distToCam = Vector3.Distance(_mainCamTransform.position, _parentPOI.transform.position);
                float scaleMultiplier = Mathf.Clamp(1.0f + (distToCam * scaleFactor), minScaleMultiplier, maxScaleMultiplier);
                visualObject.transform.localScale = _initialScale * scaleMultiplier;
            }
            else if (visualObject.transform.localScale != _initialScale)
            {
                visualObject.transform.localScale = _initialScale;
            }
        }

        private void SetVisualsVisible(bool isVisible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = isVisible ? 1.0f : 0.0f;
                _canvasGroup.interactable = isVisible;
                _canvasGroup.blocksRaycasts = isVisible;
            }

            if (_renderers != null)
            {
                foreach (var r in _renderers) if (r != null) r.enabled = isVisible;
            }
        }
    }
}
