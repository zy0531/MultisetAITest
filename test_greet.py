import os
import sys

# mock environment
os.environ["AI_MODEL"] = "qwen2.5:3b"

# set sys path so we can import app
sys.path.append("/Users/yzhao20/Documents/GitHub/rag-prototype")

from app.AI.api import triage_agent
from pprint import pprint

history = []
context = {}

try:
    res = triage_agent("Hello there!", context, history)
    pprint(res)
except Exception as e:
    import traceback
    traceback.print_exc()
