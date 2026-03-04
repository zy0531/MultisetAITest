import json
import os
import sys

# mock environment
os.environ["AI_MODEL"] = "qwen2.5:3b"

# set sys path so we can import app
sys.path.append("/Users/yzhao20/Documents/GitHub/rag-prototype")

from app.AI.api import triage_agent
from pprint import pprint

history = []
context = {
    "position": [0,0,0],
    "rotation": [0,0,0],
    "scene": "NavigationFireDynamicMesh"
}

res = triage_agent("Room 1110, room one hundred eleven, location eleven ten, destination eleven", context, history)
pprint(res)
