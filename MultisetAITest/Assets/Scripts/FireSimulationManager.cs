using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MultiSet.Navigation
{
    /// <summary>
    /// Manages the dynamic spawning of fire prefabs and ensures they block the NavMesh.
    /// </summary>
    public class FireSimulationManager : MonoBehaviour
    {
        [Header("Simulation Settings")]
        [Tooltip("List of fire prefabs that can be randomly spawned.")]
        public List<GameObject> firePrefabs = new List<GameObject>();

#if ENABLE_INPUT_SYSTEM
        [Tooltip("The keyboard key used to trigger a fire spawn.")]
        public Key spawnKey = Key.F;
#else
        [Tooltip("The keyboard key used to trigger a fire spawn.")]
        public KeyCode spawnKey = KeyCode.F;
#endif

        [Tooltip("The radius around the manager's position within which fire can spawn.")]
        public float spawnRadius = 5f;

        [Tooltip("Maximum number of active fires. Older fires will be destroyed when this limit is reached.")]
        public int maxActiveFires = 10;

        [Header("NavMesh Blocking")]
        [Tooltip("The size of the NavMeshObstacle to be added to the spawned fire.")]
        public Vector3 obstacleSize = new Vector3(1f, 2f, 1f);

        [Tooltip("Whether the NavMeshObstacle should carve the NavMesh.")]
        public bool carveNavMesh = true;

        [Header("3D Audio Persistence")]
        [Tooltip("The audio clip to play for the fire.")]
        public AudioClip fireAudioClip;

        [Range(0, 1)]
        public float audioVolume = 1.0f;

        public float audioMinDistance = 1.0f;
        public float audioMaxDistance = 15.0f;

        private List<GameObject> activeFires = new List<GameObject>();

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current[spawnKey].wasPressedThisFrame)
            {
                TriggerFireSpawn();
            }
#else
            if (Input.GetKeyDown(spawnKey))
            {
                TriggerFireSpawn();
            }
#endif
        }

        /// <summary>
        /// Public method to trigger the spawning of a random fire.
        /// Can be called by other scripts, UI buttons, or future voice commands.
        /// </summary>
        public void TriggerFireSpawn()
        {
            if (firePrefabs == null || firePrefabs.Count == 0)
            {
                Debug.LogWarning("FireSimulationManager: No fire prefabs assigned!");
                return;
            }

            Vector3 spawnPos;
            if (TryGetRandomSpawnPosition(out spawnPos))
            {
                SpawnFire(spawnPos);
            }
        }

        private bool TryGetRandomSpawnPosition(out Vector3 position)
        {
            position = Vector3.zero;
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * spawnRadius;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, spawnRadius, NavMesh.AllAreas))
            {
                position = hit.position;

                // Set Y to 1 meter below the main camera
                if (Camera.main != null)
                {
                    position.y = Camera.main.transform.position.y - 1.0f;
                }
                else
                {
                    Debug.LogWarning("FireSimulationManager: Main Camera not found. Falling back to NavMesh height.");
                }
                return true;
            }

            return false;
        }

        private int fireCounter = 0;

        private void SpawnFire(Vector3 position)
        {
            // Pick a random prefab
            GameObject prefab = firePrefabs[Random.Range(0, firePrefabs.Count)];
            
            // Instantiate
            GameObject spawnedFire = Instantiate(prefab, position, Quaternion.identity);
            spawnedFire.name = $"Fire_{fireCounter++}";

            // Ensure it has a NavMeshObstacle to block navigation
            NavMeshObstacle obstacle = spawnedFire.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = spawnedFire.AddComponent<NavMeshObstacle>();
            }

            // Configure obstacle
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = obstacleSize;
            obstacle.carving = carveNavMesh;

            // Ensure Particle Systems are looping
            ParticleSystem[] particleSystems = spawnedFire.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                main.loop = true;
                if (!ps.isPlaying) ps.Play();
            }

            // Add and configure 3D AudioSource
            if (fireAudioClip != null)
            {
                AudioSource audio = spawnedFire.AddComponent<AudioSource>();
                audio.clip = fireAudioClip;
                audio.loop = true;
                audio.playOnAwake = true;
                audio.volume = audioVolume;
                audio.spatialBlend = 1.0f; // 100% 3D
                audio.minDistance = audioMinDistance;
                audio.maxDistance = audioMaxDistance;
                audio.rolloffMode = AudioRolloffMode.Logarithmic;
                audio.Play();
            }

            // Manage active fire list
            activeFires.Add(spawnedFire);
            
            // Clean up nulls (in case fires were destroyed externally)
            activeFires.RemoveAll(f => f == null);

            // Maintain max fire limit (using while to handle runtime limit changes)
            while (activeFires.Count > maxActiveFires)
            {
                GameObject oldestFire = activeFires[0];
                activeFires.RemoveAt(0);
                
                if (oldestFire != null)
                {
                    Debug.Log($"FireSimulationManager: Destroying oldest fire: {oldestFire.name}");
                    Destroy(oldestFire);
                }
            }

            Debug.Log($"FireSimulationManager: Spawned {spawnedFire.name} at {position}. Active count: {activeFires.Count}");
        }

        /// <summary>
        /// Clears all currently active fires.
        /// </summary>
        public void ClearAllFires()
        {
            foreach (var fire in activeFires)
            {
                if (fire != null)
                {
                    Destroy(fire);
                }
            }
            activeFires.Clear();
        }
    }
}
