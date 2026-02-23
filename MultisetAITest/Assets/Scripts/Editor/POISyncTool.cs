using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using WebSocketSharp;
using System.Threading;

namespace MultiSet.Editor
{
    public class POISyncTool : EditorWindow
    {
        private string backendUrl = "ws://localhost:8000/ws/sync";
        private string jsonPath = "";
        private string statusMessage = "Select a JSON file to start.";
        private bool isSyncing = false;
        private string serverResponse = "";
        private bool waitingForResponse = false;

        [MenuItem("MultiSet/Sync POIs to Backend")]
        public static void ShowWindow()
        {
            GetWindow<POISyncTool>("POI Sync Tool");
        }

        private void OnGUI()
        {
            GUILayout.Label("POI Synchronization Settings", EditorStyles.boldLabel);
            backendUrl = EditorGUILayout.TextField("Backend WS URL", backendUrl);

            EditorGUILayout.Space();

            if (GUILayout.Button("Select POI Export JSON"))
            {
                jsonPath = EditorUtility.OpenFilePanel("Select POI Export JSON", Application.dataPath, "json");
            }

            if (!string.IsNullOrEmpty(jsonPath))
            {
                GUILayout.Label("Selected: " + Path.GetFileName(jsonPath));
            }

            EditorGUILayout.Space();

            GUI.enabled = !isSyncing && !string.IsNullOrEmpty(jsonPath);
            if (GUILayout.Button("Sync to Backend"))
            {
                SyncToBackend();
            }
            GUI.enabled = true;

            EditorGUILayout.Space();
            GUILayout.Label("Status: " + statusMessage, EditorStyles.wordWrappedLabel);

            if (!string.IsNullOrEmpty(serverResponse))
            {
                EditorGUILayout.HelpBox("Server Response: " + serverResponse, MessageType.Info);
            }
        }

        private void SyncToBackend()
        {
            if (!File.Exists(jsonPath))
            {
                statusMessage = "Error: JSON file not found!";
                return;
            }

            try
            {
                isSyncing = true;
                serverResponse = "";
                statusMessage = "Reading JSON...";

                string jsonContent = File.ReadAllText(jsonPath);
                POIExportCollection collection = JsonUtility.FromJson<POIExportCollection>(jsonContent);

                if (collection == null || collection.pois == null || collection.pois.Count == 0)
                {
                    statusMessage = "Error: Invalid or empty POI collection.";
                    isSyncing = false;
                    return;
                }

                statusMessage = $"Connecting to {backendUrl}...";

                using (var ws = new WebSocket(backendUrl))
                {
                    waitingForResponse = false;
                    
                    ws.OnOpen += (sender, e) => {
                        Debug.Log("[POISyncTool] WebSocket Open");
                    };

                    ws.OnMessage += (sender, e) => {
                        Debug.Log("[POISyncTool] Server Message: " + e.Data);
                        serverResponse = e.Data;
                        
                        // Parse status if success
                        if (e.Data.Contains("\"status\":\"success\""))
                        {
                            statusMessage = "Sync successful! Check server logs for details.";
                        }
                        else if (e.Data.Contains("\"status\":\"error\""))
                        {
                            statusMessage = "Sync failed on server.";
                        }
                        waitingForResponse = false;
                    };

                    ws.OnError += (sender, e) => {
                        Debug.LogError("[POISyncTool] WebSocket Error: " + e.Message);
                        statusMessage = "Error: " + e.Message;
                        waitingForResponse = false;
                    };

                    ws.Connect();

                    if (!ws.IsAlive)
                    {
                        statusMessage = "Error: Could not connect to backend. Is server running?";
                        isSyncing = false;
                        return;
                    }

                    int count = 0;
                    foreach (var poi in collection.pois)
                    {
                        BackendPOI backendPoi = new BackendPOI();
                        backendPoi.identification = poi.identification;
                        backendPoi.name = poi.name;
                        backendPoi.title = poi.title;
                        backendPoi.poiName = poi.poiName;
                        backendPoi.description = poi.description;
                        backendPoi.type = poi.type;
                        backendPoi.parentName = poi.parentName;
                        
                        backendPoi.position = new float[] { poi.position.x, poi.position.y, poi.position.z };
                        backendPoi.rotation = new float[] { poi.rotation.x, poi.rotation.y, poi.rotation.z };
                        backendPoi.localPosition = new float[] { poi.localPosition.x, poi.localPosition.y, poi.localPosition.z };
                        backendPoi.localRotation = new float[] { poi.localRotation.x, poi.localRotation.y, poi.localRotation.z };
                        
                        string payload = JsonUtility.ToJson(backendPoi);
                        ws.Send(payload);
                        count++;
                        statusMessage = $"Sending POI {count}/{collection.pois.Count}: {poi.poiName}";
                    }

                    // Send Commit Action
                    statusMessage = "All POIs sent. Requesting database commit...";
                    waitingForResponse = true;
                    ws.Send("{\"action\":\"commit\"}");

                    // Wait for response with timeout
                    float timeout = 60f;
                    float start = Time.realtimeSinceStartup;
                    while (waitingForResponse && (Time.realtimeSinceStartup - start) < timeout)
                    {
                        Thread.Sleep(100);
                    }

                    if (waitingForResponse)
                    {
                        statusMessage = "Error: Server timeout waiting for commit confirmation.";
                    }

                    ws.Close(CloseStatusCode.Normal, "Sync Complete");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[POISyncTool] Sync failed: " + ex.Message);
                statusMessage = "Exception: " + ex.Message;
            }
            finally
            {
                isSyncing = false;
            }
        }

        [Serializable]
        public class POIExportCollection
        {
            public string sceneName;
            public string exportTimestamp;
            public List<POIData> pois;
        }

        [Serializable]
        public class POIData
        {
            public string name;
            public string title;
            public int identification;
            public string poiName;
            public string description;
            public string type;
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 localPosition;
            public Vector3 localRotation;
            public string parentName;
        }

        [Serializable]
        public class BackendPOI
        {
            public int identification;
            public string name;
            public string title;
            public string poiName;
            public string description;
            public string type;
            public string parentName;
            public float[] position;
            public float[] rotation;
            public float[] localPosition;
            public float[] localRotation;
        }
    }
}
