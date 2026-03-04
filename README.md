# MultisetAITest

## Main Application Scene
The main working Unity scene for navigation is located at:
`Assets/Samples/MultiSet Quest SDK/1.9.2/Sample Scenes/Navigation/NavigationFireDynamicMesh.unity`

## POI Management
This project includes a custom editor tool to extract Points of Interest (POIs) from the Unity scene and synchronize them with the backend database. 

### Exporting POIs
1. In the Unity Editor top menu, go to **POITools -> Export POIs to JSON**.
2. This will parse all the active POIs in the scene and save them to a local JSON file.
3. The exported JSON file is located at: `MultisetAITest/Assets/AtriumBuildingPOIs.json`

### Syncing POIs to Backend
1. Once exported, you can sync the data to the external RAG backend.
2. In the Unity Editor top menu, go to **POITools -> Sync POIs to Backend**.
3. This sends an HTTP request to the backend server with the extracted POI data to populate the vector database for the AI assistant.

## Querying the AI
To send a query to the AI navigation system, use the `RealTimeQueryManager` component attached to the `RealTimeQuery` GameObject in the scene hierarchy.

### Example Query
1. Specific/Generic
1.1 Specific: Guide me to room 1050
1.2 Generic: Guide me to grab some coffee
2. Single/Multi-Step
2.1 Single: Guide me to room 1050
2.2 Multi-Step: Guide me to room 1050, then grab some coffee; Navigate me to room 1050 first and to 2014
3. Hallucination
3.1 Existent POI: Guide me to room 1050
3.2 Non-Existent POI: Guide me to room 10500


