using UnityEngine;

namespace MultiSet.Samples
{
    /// <summary>
    /// Smooths the position and rotation of the object it is attached to.
    /// Intended to be used on the AR Camera or Localization Root to filter out jitter and sudden jumps.
    /// Should run AFTER the localization manager updates the transform (use Script Execution Order if needed).
    /// </summary>
    public class LocalizationSmoother : MonoBehaviour
    {
        [Header("Smoothing Settings")]
        [Tooltip("Smoothing factor for position (0 = frozen, 1 = no smoothing). Lower is smoother.")]
        [Range(0f, 1f)]
        public float positionSmoothFactor = 0.1f;

        [Tooltip("Smoothing factor for rotation (0 = frozen, 1 = no smoothing). Lower is smoother.")]
        [Range(0f, 1f)]
        public float rotationSmoothFactor = 0.1f;

        [Header("Outlier Rejection")]
        [Tooltip("If the jump is larger than this (meters), we snap immediately (teleport) to avoid trailing behind.")]
        public float snapDistanceThreshold = 5.0f;

        [Tooltip("If the jump is larger than this (meters) but smaller than snap, we consider it an outlier and ignore it for a few frames.")]
        public float outlierDistanceThreshold = 0.5f;

        [Tooltip("How many consecutive outlier frames to ignore before accepting the new position.")]
        public int maxOutlierFrames = 5;

        private Vector3 _lastSmoothedPos;
        private Quaternion _lastSmoothedRot;
        private bool _isInitialized = false;
        private int _outlierCount = 0;

        void OnEnable()
        {
            _isInitialized = false;
            _outlierCount = 0;
        }

        void LateUpdate()
        {
            // The manager has already updated transform.position/rotation in Update()
            Vector3 rawPos = transform.position;
            Quaternion rawRot = transform.rotation;

            if (!_isInitialized)
            {
                _lastSmoothedPos = rawPos;
                _lastSmoothedRot = rawRot;
                _isInitialized = true;
                return;
            }

            float dist = Vector3.Distance(rawPos, _lastSmoothedPos);

            if (dist > snapDistanceThreshold)
            {
                // Large jump: Teleport (Snap)
                _lastSmoothedPos = rawPos;
                _lastSmoothedRot = rawRot;
                _outlierCount = 0;
            }
            else if (dist > outlierDistanceThreshold)
            {
                // Medium jump: Potential Outlier
                if (_outlierCount < maxOutlierFrames)
                {
                    // Ignore this update, revert to last smoothed
                    _outlierCount++;
                    transform.position = _lastSmoothedPos;
                    transform.rotation = _lastSmoothedRot;
                    return;
                }
                else
                {
                    // We've ignored it long enough, it's probably real. Snap or smooth towards it.
                    // Let's smooth towards it to avoid a visual pop, but reset outlier count.
                    _outlierCount = 0;
                    // Fall through to smoothing
                }
            }
            else
            {
                // Normal movement: Reset outlier count
                _outlierCount = 0;
            }

            // Apply Smoothing
            // Use Lerp for simple exponential smoothing
            // Note: Frame-rate independent smoothing would use: Vector3.Lerp(a, b, 1 - Mathf.Exp(-decay * Time.deltaTime))
            // But for simple jitter reduction, fixed factor is often enough if frame rate is stable.
            // We'll use Time.deltaTime to be safe.
            
            float posT = 1f - Mathf.Exp(-positionSmoothFactor * 60f * Time.deltaTime); // Approximate 60fps reference
            float rotT = 1f - Mathf.Exp(-rotationSmoothFactor * 60f * Time.deltaTime);

            _lastSmoothedPos = Vector3.Lerp(_lastSmoothedPos, rawPos, posT);
            _lastSmoothedRot = Quaternion.Slerp(_lastSmoothedRot, rawRot, rotT);

            // Apply smoothed values back to transform
            transform.position = _lastSmoothedPos;
            transform.rotation = _lastSmoothedRot;
        }
    }
}
