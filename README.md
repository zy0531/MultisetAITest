# MultisetAITest

## Main Application Scene
The main working Unity scene for navigation is located at:
`Assets/Samples/MultiSet Quest SDK/1.9.2/Sample Scenes/Navigation/NavigationFireDynamicMesh.unity`

## POI Management
This project includes a custom editor tool to extract Points of Interest (POIs) from the Unity scene and synchronize them with the backend database. 

### Exporting POIs
1. In the Unity Editor top menu, go to **Tools -> Export POIs to JSON**.
2. This will parse all the active POIs in the scene and save them to a local JSON file.
3. The exported JSON file is located at: `MultisetAITest/Assets/NavigationFireDynamicMesh_POIs.json`

### Syncing POIs to Backend
1. Once exported, you can sync the data to the external RAG backend.
2. In the Unity Editor top menu, go to **Tools -> Sync POIs to Backend**.
3. This sends an HTTP request to the backend server with the extracted POI data to populate the vector database for the AI assistant.