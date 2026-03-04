import json

content = """{
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
}"""

try:
    json.loads(content)
    print("VALID JSON")
except Exception as e:
    print("INVALID JSON:", e)
