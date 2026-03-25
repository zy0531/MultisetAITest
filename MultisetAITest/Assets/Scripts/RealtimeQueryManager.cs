using UnityEngine;
using UnityEngine.AI;
using WebSocketSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class RealtimeQueryManager : MonoBehaviour
{
    [Header("Configuration")]
    public string backendUrl = "ws://localhost:8000/ws/AI";
    public bool connectOnStart = true;
    public Transform trackingTarget;

    [Header("Testing")]
    public string manualQuery = "Where is the nearest exit?";
    private WebSocket ws;
    private string lastResponse = "";
    private string lastQuery = "";
    private bool isQuerying = false;

    // Thread-safe queue to process network events on the main thread
    public ConcurrentQueue<string> responseQueue = new ConcurrentQueue<string>();

    // Local cache for resolving IDs to scene objects
    private Dictionary<int, MonoBehaviour> poiCache = new Dictionary<int, MonoBehaviour>();
    
    // Track spawned line visualization objects
    private List<GameObject> activePathVisualizations = new List<GameObject>();

    [Serializable]
    public class QueryRequest
    {
        public string query;
        public UserContext context;
    }

    [Serializable]
    public class UserContext
    {
        public float[] position;
        public float[] rotation;
        public string scene;
    }

    [Serializable]
    public class TriageResponse
    {
        public string type;
        public string response; // For greetings/inquiry
        public List<TargetInfo> targets;
        public List<AIAction> actions; // New: Agentic actions
        public string message; // For errors or status
    }

    [Serializable]
    public class AIAction
    {
        public string cmd;
        public int id;
        public string target_name;
        public string text;
        public string reason;
    }

    [Serializable]
    public class TargetInfo
    {
        public string category;
        public string semantics;
        public string description;
        public List<POIResult> poi_results;
    }

    [Serializable]
    public class POIResult
    {
        public int id;
        public string name;
    }

    [Serializable]
    public class VerificationRequest
    {
        public string type;
        public string original_type;
        public string query;
        public List<VerificationTarget> targets;
    }

    [Serializable]
    public class VerificationTarget
    {
        public string semantics;
        public List<VerificationPoi> poi_results;
    }

    [Serializable]
    public class VerificationPoi
    {
        public int id;
        public string name;
        public float distance;
    }

    protected virtual void Start()
    {
        RefreshPOICache();
        if (connectOnStart)
        {
            Connect();
        }
    }

    public void RefreshPOICache()
    {
        poiCache.Clear();
        // Find all objects with a script named "POI" using reflection-lite approach
        MonoBehaviour[] allScripts = FindObjectsOfType<MonoBehaviour>();
        foreach (var script in allScripts)
        {
            if (script.GetType().Name == "POI")
            {
                // We use reflection to get the 'identification' field
                var idField = script.GetType().GetField("identification", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (idField != null)
                {
                    int id = (int)idField.GetValue(script);
                    if (!poiCache.ContainsKey(id)) poiCache.Add(id, script);
                }
            }
        }
        Debug.Log($"[RealtimeQueryManager] Cached {poiCache.Count} POIs from the scene.");
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    public void Connect()
    {
        if (ws != null) return;

        ws = new WebSocket(backendUrl);

        ws.OnOpen += (sender, e) => {
            Debug.Log("[RealtimeQueryManager] Connected to backend.");
        };

        ws.OnMessage += (sender, e) => {
            // Queue the message to be processed on Unity's main thread
            responseQueue.Enqueue(e.Data);
        };

        ws.OnError += (sender, e) => {
            Debug.LogError("[RealtimeQueryManager] WebSocket Error: " + e.Message);
            isQuerying = false;
        };

        ws.OnClose += (sender, e) => {
            Debug.Log("[RealtimeQueryManager] Connection closed.");
        };

        ws.Connect();
    }

    protected virtual void Update()
    {
        // Process scheduled messages on the main thread
        while (responseQueue.TryDequeue(out string data))
        {
            Debug.Log("[RealtimeQueryManager] Processing Response on Main Thread: " + data);
            lastResponse = data;
            ProcessResponse(data);
        }
    }

    public void Disconnect()
    {
        if (ws != null)
        {
            ws.Close();
            ws = null;
        }
    }

    public void SendQuery(string userQuery)
    {
        if (ws == null || !ws.IsAlive)
        {
            Debug.LogWarning("[RealtimeQueryManager] Not connected. Attempting to reconnect...");
            Connect();
            if (ws == null || !ws.IsAlive) return;
        }

        isQuerying = true;
        
        // Cleanup old visualizations
        foreach (var oldPath in activePathVisualizations)
        {
            if (oldPath != null) Destroy(oldPath);
        }
        activePathVisualizations.Clear();
        
        // Resolve target transform: assigned target > main camera > this transform
        Transform target_trans = trackingTarget;
        if (target_trans == null && Camera.main != null) target_trans = Camera.main.transform;
        if (target_trans == null) target_trans = this.transform;

        // Capture context systematically
        UserContext context = new UserContext
        {
            position = new float[] { target_trans.position.x, target_trans.position.y, target_trans.position.z },
            rotation = new float[] { target_trans.rotation.eulerAngles.x, target_trans.rotation.eulerAngles.y, target_trans.rotation.eulerAngles.z },
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        };

        lastQuery = userQuery;

        QueryRequest request = new QueryRequest 
        { 
            query = userQuery,
            context = context
        };

        string payload = JsonUtility.ToJson(request);
        ws.Send(payload);
        Debug.Log($"[RealtimeQueryManager] Sent query: {userQuery} with context at {target_trans.position}");
    }

    private void ProcessResponse(string json)
    {
        isQuerying = false;
        try
        {
            TriageResponse response = JsonUtility.FromJson<TriageResponse>(json);
            
            if (!string.IsNullOrEmpty(response.response))
            {
                Debug.Log("[RealtimeQueryManager] AI: " + response.response);
            }

            // Step 1: Handle Initial Request (Both Navigation and Inquiry now get verified!)
            if ((response.type == "navigation" || response.type == "inquiry") && response.targets != null && response.targets.Count > 0)
            {
                VerificationRequest vRequest = CalculateNavMeshDistances(response.targets);
                vRequest.query = lastQuery;
                vRequest.original_type = response.type; // Tell Python STAGE 5 what intent we are fulfilling
                
                string vPayload = JsonUtility.ToJson(vRequest);
                
                Debug.Log($"[RealtimeQueryManager] Sending 2-Way Verification Payload: {vPayload}");
                ws.Send(vPayload);
                return; // Stop execution until Verification responds
            }

            // Step 2: Receive the final verified AI actions
            if (response.actions != null && response.actions.Count > 0)
            {
                Vector3 currentStartPos = trackingTarget != null ? trackingTarget.position 
                                        : (Camera.main != null ? Camera.main.transform.position 
                                        : transform.position);

                foreach (var action in response.actions)
                {
                    // For the final step, we just execute the action the AI gave us
                    // We can resolve the MonoBehaviour if we want, but ID is enough for execution
                    MonoBehaviour bestPOI = action.id > 0 && poiCache.ContainsKey(action.id) ? poiCache[action.id] : null;

                    if (bestPOI != null && (action.cmd == "navigation" || action.cmd == "inquiry"))
                    {
                        Vector3 validStart = currentStartPos;
                        Vector3 validEnd = bestPOI.transform.position;
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(currentStartPos, out hit, 2.5f, NavMesh.AllAreas)) validStart = hit.position;
                        if (NavMesh.SamplePosition(bestPOI.transform.position, out hit, 2.5f, NavMesh.AllAreas)) validEnd = hit.position;

                        NavMeshPath path = new NavMeshPath();
                        // Allow PathPartial so it doesn't fail silently over slight navigation gaps
                        if (NavMesh.CalculatePath(validStart, validEnd, NavMesh.AllAreas, path) && path.status != NavMeshPathStatus.PathInvalid)
                        {
                            DrawPathInGame(path, Color.magenta, 60f, validEnd);
                            
                            // Set the next path to start from this destination!
                            currentStartPos = validEnd;
                        }
                        else
                        {
                            Debug.LogWarning($"[RealtimeQueryManager] Could not calculate valid NavMesh route for {bestPOI.gameObject.name}. Drawing straight-line fallback.");
                            DrawStraightLineInGame(validStart, validEnd, new Color(1f, 0.5f, 0f), 60f); // Bright Orange
                            currentStartPos = validEnd; // Still advance point in case next path succeeds
                        }
                    }

                    ExecuteAction(action, bestPOI);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[RealtimeQueryManager] Failed to parse response: " + ex.Message);
        }
    }

    private VerificationRequest CalculateNavMeshDistances(List<TargetInfo> targets)
    {
        VerificationRequest req = new VerificationRequest();
        req.type = "verification";
        req.targets = new List<VerificationTarget>();

        Vector3 currentStartPos = trackingTarget != null ? trackingTarget.position 
                        : (Camera.main != null ? Camera.main.transform.position 
                        : transform.position);

        foreach (var target in targets)
        {
            if (target.poi_results == null || target.poi_results.Count == 0) continue;

            VerificationTarget vTarget = new VerificationTarget();
            vTarget.semantics = target.semantics;
            vTarget.poi_results = new List<VerificationPoi>();

            MonoBehaviour closestPOI = null;
            float minDistance = float.MaxValue;
            NavMeshPath bestPath = null;

            foreach (var poiResult in target.poi_results)
            {
                if (poiCache.TryGetValue(poiResult.id, out MonoBehaviour poiScript))
                {
                    Vector3 destination = poiScript.transform.position;
                    float pathLength = float.MaxValue;

                    // Ensure both starting and ending points are physically on the NavMesh
                    Vector3 validStart = currentStartPos;
                    Vector3 validEnd = destination;
                    NavMeshHit hit;
                    
                    if (NavMesh.SamplePosition(currentStartPos, out hit, 2.5f, NavMesh.AllAreas)) validStart = hit.position;
                    if (NavMesh.SamplePosition(destination, out hit, 2.5f, NavMesh.AllAreas)) validEnd = hit.position;

                    // Support PathComplete AND PathPartial so stairs/door links don't randomly fail
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(validStart, validEnd, NavMesh.AllAreas, path) 
                        && path.status != NavMeshPathStatus.PathInvalid)
                    {
                        pathLength = GetPathLength(path, validEnd);
                    }
                    else
                    {
                        // Fallback only if no physical nav path exists
                        pathLength = Vector3.Distance(validStart, validEnd);
                        Debug.LogWarning($"[RealtimeQueryManager] NavMesh path to ID {poiResult.id} not found entirely, utilizing physical straight-line distance.");
                    }
                    
                    // Add physical distance to verification payload for AI
                    vTarget.poi_results.Add(new VerificationPoi {
                        id = poiResult.id,
                        name = poiResult.name,
                        distance = pathLength
                    });

                    if (pathLength < minDistance)
                    {
                        minDistance = pathLength;
                        closestPOI = poiScript;
                        bestPath = path;
                    }
                }
                else
                {
                    Debug.LogWarning($"[RealtimeQueryManager] Target ID {poiResult.id} not found in scene cache.");
                }
            }
            
            // Re-anchor the next distance check to start from the winning POI!
            if (closestPOI != null)
            {
                currentStartPos = closestPOI.transform.position;
            }
            
            req.targets.Add(vTarget);

        }

        return req;
    }

    private float GetPathLength(NavMeshPath path, Vector3 trueDestination)
    {
        float length = 0f;
        Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
        {
            length += Vector3.Distance(corners[i - 1], corners[i]);
        }
        
        // If it's a partial path (e.g. across floors without stairs), append the physical drop gap
        if (path.status == NavMeshPathStatus.PathPartial && corners.Length > 0)
        {
            length += Vector3.Distance(corners[corners.Length - 1], trueDestination);
        }
        return length;
    }

    private void DrawPathInGame(NavMeshPath path, Color color, float durationSecs, Vector3 trueDestination)
    {
        // Create an empty GameObject to hold the line renderer
        GameObject lineObj = new GameObject("AI_Path_Visualization");
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        
        bool addGapDrop = path.status == NavMeshPathStatus.PathPartial && path.corners.Length > 0;
        int count = path.corners.Length + (addGapDrop ? 1 : 0);
        
        line.positionCount = count;
        Vector3[] offsetCorners = new Vector3[count];
        
        for (int i = 0; i < path.corners.Length; i++)
        {
            // Raises the line slightly so it's clearly visible above the floor
            offsetCorners[i] = path.corners[i] + Vector3.up * 0.5f;
        }

        if (addGapDrop)
        {
            // Appends a visible line connecting the dead-end NavMesh boundary directly to the destination
            offsetCorners[count - 1] = trueDestination + Vector3.up * 0.5f;
        }
        
        line.SetPositions(offsetCorners);
        
        // Setup LineRenderer properties to make it highly visible
        line.startWidth = 0.2f;
        line.endWidth = 0.2f;
        line.material = new Material(Shader.Find("Sprites/Default")); // Basic unlit shader
        line.startColor = color;
        line.endColor = color;
        
        activePathVisualizations.Add(lineObj);
        
        // Auto-cleanup the visualization line after duration
        Destroy(lineObj, durationSecs);
    }

    private void DrawStraightLineInGame(Vector3 start, Vector3 end, Color color, float durationSecs)
    {
        GameObject lineObj = new GameObject("AI_Path_Visualization_Fallback");
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        
        line.positionCount = 2;
        line.SetPositions(new Vector3[] { start + Vector3.up * 0.5f, end + Vector3.up * 0.5f });
        
        line.startWidth = 0.2f;
        line.endWidth = 0.2f;
        line.material = new Material(Shader.Find("Sprites/Default")); // Basic unlit shader
        line.startColor = color;
        line.endColor = color;
        
        activePathVisualizations.Add(lineObj);
        Destroy(lineObj, durationSecs);
    }

    private void ExecuteAction(AIAction action, MonoBehaviour bestPOIContext)
    {
        int targetID = action.id;
        
        // If the action ID is 0 or -1, use the best global POI we found
        if (targetID <= 0 && bestPOIContext != null)
        {
            // Try to extract ID from the context
            var idField = bestPOIContext.GetType().GetField("identification", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (idField != null) targetID = (int)idField.GetValue(bestPOIContext);
        }

        Debug.Log($"[RealtimeQueryManager] Executed Action Category: {action.cmd} (ID Target: {targetID})");
        
        switch (action.cmd)
        {
            case "navigation":
                if (poiCache.ContainsKey(targetID))
                    Debug.Log($"[RealtimeQueryManager] Logic: Start Pathfinding to {poiCache[targetID].gameObject.name}");
                else
                    Debug.LogWarning($"[RealtimeQueryManager] Action Target ID {targetID} not found in scene cache.");
                break;
            case "inquiry":
                if (poiCache.ContainsKey(targetID))
                    Debug.Log($"[RealtimeQueryManager] Logic: Show Details for {poiCache[targetID].gameObject.name}");
                break;
            case "greeting":
                Debug.Log("[RealtimeQueryManager] Logic: Play Social Animation/Audio");
                break;
            case "others":
                Debug.Log("[RealtimeQueryManager] Logic: Handle General Query");
                break;
        }
    }

    // Helper for testing in Inspector
    [ContextMenu("Send Manual Query")]
    public void SendManualQuery()
    {
        if (!string.IsNullOrEmpty(manualQuery))
        {
            SendQuery(manualQuery);
        }
    }

    [ContextMenu("Test Query - Coffee")]
    public void TestQueryCoffee()
    {
        SendQuery("Where can I get a coffee?");
    }

    [ContextMenu("Test Query - Meeting")]
    public void TestQueryMeeting()
    {
        SendQuery("Take me to the nearest meeting room.");
    }
}
