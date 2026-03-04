import json
import traceback

data = {
  "type": "navigation",
  "response": "",
  "targets": [
    {
      "target_type": "specific",
      "semantics": "Room 1110, room one hundred eleven, location eleven ten, destination eleven",
      "filter": "name LIKE '%1110%'"
    }
  ],
  "actions": [
    {"cmd": "navigation", "id": 0}
  ]
}

def mock_search_poi(semantics, count, fields, filter_expression=""):
    # Returns [(dict, float)]
    return [
       ({"id": 53, "name": "Room 1110", "poiName": "Room 1110", "type": "Room"}, 0.25),
       ({"id": 33, "name": "Room 1211", "poiName": "Room 1211", "type": "Room"}, 0.2)
    ]

try:
    for target in data["targets"]:
        semantics = target.get("semantics", "")
        search_filter = target.get("filter", "")
        target_type = target.get("target_type", "generic")
        fetch_count = 5
        raw_results = mock_search_poi(semantics, fetch_count, [], search_filter)
        
        if raw_results:
            candidate_strings = []
            for res in raw_results:
                item = res[0]
                candidate_strings.append(f"ID: {item.get('id')} | Name: {item.get('name')} | Local Name: {item.get('poiName')} | Type: {item.get('type')}")
            candidate_text = "\n".join(candidate_strings)
            
            # MOCK OLLAMA CRASH OR SUCCESS
            valid_ids = [53] # mock response
            if valid_ids and isinstance(valid_ids, list):
                try:
                    valid_ids = [int(v) for v in valid_ids]
                    raw_results = [r for r in raw_results if r[0].get("id") in valid_ids]
                except Exception as e:
                    print("IGNORE EXC:", e)
                    pass
            print(raw_results)
            
        # DANGER ZONE
        target["poi_results"] = [{"id": res[0].get("id", 0), "name": res[0].get("name", "Unknown")} for res in raw_results]

    print("Success target:", data)

except Exception as e:
    traceback.print_exc()

