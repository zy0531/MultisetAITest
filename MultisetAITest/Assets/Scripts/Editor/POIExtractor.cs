using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System;

namespace MultiSet.Samples.Editor
{
    public class POIExtractor : EditorWindow
    {
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
        public class POIExportCollection
        {
            public string sceneName;
            public string exportTimestamp;
            public List<POIData> pois = new List<POIData>();
        }

        [MenuItem("MultiSet/Export POIs to JSON")]
        public static void ExportPOIs()
        {
            POIExportCollection collection = new POIExportCollection();
            collection.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            collection.exportTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Find all objects of type POI in the scene
            MonoBehaviour[] allScripts = FindObjectsOfType<MonoBehaviour>();
            foreach (var script in allScripts)
            {
                if (script.GetType().Name == "POI")
                {
                    POIData data = new POIData();
                    data.name = script.gameObject.name;
                    
                    // Extract fields from POI script using reflection
                    data.identification = Convert.ToInt32(GetFieldValue(script, "identification") ?? "0");
                    data.poiName = GetFieldValue(script, "poiName")?.ToString() ?? "";
                    data.description = GetFieldValue(script, "description")?.ToString() ?? "";
                    data.type = GetFieldValue(script, "type")?.ToString() ?? "None";
                    
                    // listTitle is typically from the base class ListItemData
                    data.title = GetFieldValue(script, "listTitle")?.ToString() ?? data.poiName;
                    if (string.IsNullOrEmpty(data.title)) data.title = script.gameObject.name;
                    
                    data.position = script.transform.position;
                    data.rotation = script.transform.eulerAngles;
                    data.localPosition = script.transform.localPosition;
                    data.localRotation = script.transform.localEulerAngles;
                    data.parentName = script.transform.parent != null ? script.transform.parent.name : "None";
                    
                    collection.pois.Add(data);
                }
            }

            if (collection.pois.Count == 0)
            {
                EditorUtility.DisplayDialog("Export POIs", "No POIs found in the current scene.", "OK");
                return;
            }

            string json = JsonUtility.ToJson(collection, true);
            string path = EditorUtility.SaveFilePanel("Save POIs to JSON", "Assets", collection.sceneName + "_POIs.json", "json");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, json);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Export POIs", $"Successfully exported {collection.pois.Count} POIs to {path}", "OK");
                Debug.Log($"[POIExtractor] Exported {collection.pois.Count} POIs to {path}");
            }
        }

        private static object GetFieldValue(object obj, string memberName)
        {
            try
            {
                var type = obj.GetType();
                
                // Try fields first (most common for public inspector variables)
                var field = type.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) return field.GetValue(obj);

                // Try properties
                var prop = type.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null) return prop.GetValue(obj);
                
                // Search base classes manually if needed (reflection usually handles this but let's be safe)
                // especially for inherited fields like listTitle
                var baseType = type.BaseType;
                while (baseType != null)
                {
                    field = baseType.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null) return field.GetValue(obj);
                    
                    prop = baseType.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (prop != null) return prop.GetValue(obj);
                    
                    baseType = baseType.BaseType;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[POIExtractor] Error getting member {memberName}: {e.Message}");
            }
            return null;
        }
    }
}
