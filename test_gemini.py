import os
import sys

# mock environment
os.environ["AI_MODEL"] = "gemini-2.5-flash"
os.environ["GEMINI_API_KEY"] = "MOCK_KEY_FOR_SYNTAX_TEST"

# set sys path so we can import app
sys.path.append("/Users/yzhao20/Documents/GitHub/rag-prototype")

from app.AI.api import generate_chat_response

# test basic gemini routing
try:
    generate_chat_response("gemini-2.5-flash", [{"role": "user", "content": "hi"}])
except Exception as e:
    print("Error (Expected auth error but checking syntax):", e)
    
print("Syntax OK")
