import sys
sys.path.append("/Users/yzhao20/Documents/GitHub/rag-prototype")
from app.dependencies import get_db_gen

try:
    with get_db_gen() as db:
        res = db.query(collection_name="poi", filter="id == 54", output_fields=["name"])
        print("Does ID 54 exist?:", res)
        # also print all names that have 1110 
        res2 = db.query(collection_name="poi", filter="name LIKE 'Room 111%'", output_fields=["id", "name"])
        print("Any with 111 in name?:", res2)
except Exception as e:
    import traceback
    traceback.print_exc()
