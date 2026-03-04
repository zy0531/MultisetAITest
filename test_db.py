import sys
sys.path.append("/Users/yzhao20/Documents/GitHub/rag-prototype")
from app.database.db import search_poi
from app.dependencies import get_db_gen
import time

res = search_poi("room 1110", 20, ["id", "name", "poiName", "type"])
print("Semantic Top 20:")
for hit in res:
   print(hit)

try:
    with get_db_gen() as db:
        print("\nDoes LIKE work?")
        res2 = db.search(
            collection_name="poi", 
            data=[[0.1]*1536], # dummy vector
            limit=50, 
            output_fields=["id", "name", "poiName"], 
            filter="name LIKE 'Room 1110%'"
        )
        print("Prefix match:", res2)
        
        res3 = db.search(
            collection_name="poi", 
            data=[[0.1]*1536], 
            limit=50, 
            output_fields=["id", "name", "poiName"], 
            filter="name LIKE '%1110%'"
        )
        print("Infix match:", res3)
except Exception as e:
    import traceback
    traceback.print_exc()
