import json
import sys
sys.path.append("/Users/yzhao20/Documents/GitHub/rag-prototype")
from app.dependencies import get_db_gen
from app.database.db import ensure_collection
from app.poi.models import get_poi_schema, get_index_params
from app.database.db import embedding_fn

def sync_pois():
    with open("/Users/yzhao20/Documents/GitHub/MultisetAITest/MultisetAITest/Assets/NavigationFireDynamicMesh_POIs.json", "r") as f:
        data = json.load(f)
    print(f"Loaded {len(data['pois'])} POIs from JSON.")
    
    pois_to_process = []
    texts_to_embed = []
    
    from app.websockets.api import POI
    
    for item in data['pois']:
        poi_args = {
            "id": item.get("identification", 0),
            "name": item.get("name", ""),
            "title": item.get("title", ""),
            "poiName": item.get("poiName", ""),
            "description": item.get("description", ""),
            "type": item.get("type", "Room"),
            "parentName": item.get("parentName", ""),
            "position": item.get("position", [0.0, 0.0, 0.0]),
            "rotation": item.get("rotation", [0.0, 0.0, 0.0]),
            "localPosition": item.get("localPosition", [0.0, 0.0, 0.0]),
            "localRotation": item.get("localRotation", [0.0, 0.0, 0.0]),
            "vector": item.get("vector", None)
        }
        
        for field in ["position", "rotation", "localPosition", "localRotation"]:
            val = item.get(field)
            if isinstance(val, dict):
                poi_args[field] = [val.get("x", 0.0), val.get("y", 0.0), val.get("z", 0.0)]
                
        poi = POI(**poi_args)
        pois_to_process.append(poi)
        
        text = f"The {poi.type} named '{poi.poiName}' (also known as {poi.name}) is a point of interest titled '{poi.title}'. " \
               f"It is situated within the {poi.parentName if poi.parentName else 'main scene'}. " \
               f"Description of this area: {poi.description if poi.description else 'No specific details provided.'}"
        texts_to_embed.append(text)
        
    print("Generating embeddings...")
    vectors = embedding_fn.encode_documents(texts_to_embed)
    
    deduped_data = {}
    for i, poi in enumerate(pois_to_process):
        poi.vector = vectors[i].tolist() if hasattr(vectors[i], "tolist") else list(vectors[i])
        dump = poi.model_dump()
        deduped_data[dump["id"]] = dump
        
    data_to_insert = list(deduped_data.values())
    
    print("Upserting into Milvus...")
    with get_db_gen() as db:
        res = db.upsert(
            collection_name="poi",
            data=data_to_insert
        )
        print(f"Upserted. {res}")

if __name__ == "__main__":
    sync_pois()
