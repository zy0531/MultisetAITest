using UnityEngine;
using UnityEngine.AI;

namespace MultiSet.Samples
{
    /// <summary>
    /// Controls the robot character for "Robot Navigation" mode.
    /// Moves to the destination, waits, and then returns to the user.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class RobotNavigator : MonoBehaviour
    {
        public enum RobotState
        {
            Idle,
            ToDestination,
            WaitingAtDestination,
            ReturningToUser
        }

        [Header("References")]
        [Tooltip("Reference to the main NavigationController to get destination info.")]
        public NavigationController navigationController;

        [Tooltip("The camera or object representing the user's position.")]
        public Transform userTransform;

        [Header("Settings")]
        public float arrivalDistance = 1.0f;
        public float waitTimeAtDestination = 2.0f;

        private NavMeshAgent _agent;
        private RobotState _currentState = RobotState.Idle;
        private float _waitTimer = 0f;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            // Disable auto-braking to allow smoother arrival if needed, 
            // but for simple point-to-point, default is fine.
        }

        void Start()
        {
            if (navigationController == null)
            {
                navigationController = FindObjectOfType<NavigationController>();
            }

            if (userTransform == null)
            {
                if (Camera.main != null)
                {
                    userTransform = Camera.main.transform;
                }
            }
        }

        void Update()
        {
            switch (_currentState)
            {
                case RobotState.ToDestination:
                    CheckArrivalAtDestination();
                    break;
                case RobotState.WaitingAtDestination:
                    UpdateWaitTimer();
                    break;
                case RobotState.ReturningToUser:
                    UpdateReturnTarget();
                    CheckArrivalAtUser();
                    break;
                case RobotState.Idle:
                default:
                    break;
            }
        }

        /// <summary>
        /// Starts the robot navigation sequence to the current destination set in NavigationController.
        /// </summary>
        public void StartRobotNavigation()
        {
            if (navigationController == null || navigationController.currentDestination == null)
            {
                Debug.LogWarning("RobotNavigator: No destination set in NavigationController.");
                return;
            }

            Vector3 destPos = navigationController.currentDestination.poiCollider.transform.position;
            _agent.SetDestination(destPos);
            _agent.isStopped = false;
            _currentState = RobotState.ToDestination;
            Debug.Log($"RobotNavigator: Going to {navigationController.currentDestination.poiName}");
        }

        /// <summary>
        /// Stops the robot immediately.
        /// </summary>
        public void StopRobot()
        {
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
            }
            _currentState = RobotState.Idle;
        }

        private void CheckArrivalAtDestination()
        {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + arrivalDistance)
            {
                // Arrived
                Debug.Log("RobotNavigator: Arrived at destination. Waiting...");
                _currentState = RobotState.WaitingAtDestination;
                _waitTimer = waitTimeAtDestination;
            }
        }

        private void UpdateWaitTimer()
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                Debug.Log("RobotNavigator: Returning to user.");
                _currentState = RobotState.ReturningToUser;
                UpdateReturnTarget();
            }
        }

        private void UpdateReturnTarget()
        {
            if (userTransform != null)
            {
                _agent.SetDestination(userTransform.position);
                _agent.isStopped = false;
            }
        }

        private void CheckArrivalAtUser()
        {
            // Continuously update destination to user's position in UpdateReturnTarget if user moves?
            // Yes, calling UpdateReturnTarget() every frame in Update() handles moving user.
            
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + arrivalDistance)
            {
                // Returned to user
                Debug.Log("RobotNavigator: Returned to user.");
                _currentState = RobotState.Idle;
                // Optional: Face the user?
            }
        }
    }
}
