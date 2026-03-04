import json

data = json.loads("""{
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
}""")

def json_serializable(data):
    if isinstance(data, dict):
        return {k: json_serializable(v) for k, v in data.items()}
    elif isinstance(data, list):
        return [json_serializable(item) for item in data]
    return data

user_query = "Take me to room 1110"
raw_results = [
    ({"id": 53, "name": "Room 1110", "poiName": "Room 1110", "type": "Room"}, 0.25),
    ({"id": 33, "name": "Room 1211", "poiName": "Room 1211", "type": "Room"}, 0.2)
]

try:
    for target in data["targets"]:
        semantics = target.get("semantics", "")
        if semantics:
            search_filter = target.get("filter", "")
            target_type = target.get("target_type", "generic")
            fetch_count = 5
            
            # --- AI Reranking / Validation ---
            if raw_results:
                candidate_strings = []
                for res in raw_results:
                    item = res[0]
                    candidate_strings.append(f"ID: {item.get('id')} | Name: {item.get('name')} | Local Name: {item.get('poiName')} | Type: {item.get('type')}")
                candidate_text = "\n".join(candidate_strings)
                
                sys_prompt = f"""You are a spatial filter. The user requested to navigate: "{user_query}"\n\nHere are the top candidates retrieved from the building database:\n{candidate_text}"""
                
                try:
                    # mock Ollama response
                    val_response = {'message': {'content': '[53]'}}
                    valid_ids = json.loads(val_response['message']['content'])
                    
                    if valid_ids and isinstance(valid_ids, list):
                        try:
                            valid_ids = [int(v) for v in valid_ids]
                            raw_results = [r for r in raw_results if r[0].get("id") in valid_ids]
                        except Exception:
                            pass
                except Exception as e:
                    raw_results = raw_results[:1]
                    
            target["poi_results"] = [{"id": res[0].get("id", 0), "name": res[0].get("name", "Unknown")} for res in raw_results]
    
    final_data = json_serializable(data)
    print("SUCCESS")
except Exception as e:
    import traceback
    traceback.print_exc()

