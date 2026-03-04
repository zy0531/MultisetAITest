import os
import sys

# mock environment
os.environ["AI_MODEL"] = "gemini-2.5-flash"
os.environ["GEMINI_API_KEY"] = "MOCK_KEY_FOR_SYNTAX_TEST"

# set sys path so we can import app
sys.path.append("/Users/yzhao20/Documents/GitHub/rag-prototype")

def test_syntax():
    model_name = "gemini-2.5-flash"
    messages = [{"role": "system", "content": "hello"}, {"role": "user", "content": "hi"}]
    format = "json"
    
    from google import genai
    from google.genai import types
    api_key = os.environ.get("GEMINI_API_KEY", "")
    client = genai.Client(api_key=api_key)
    
    system_instruction = next((msg["content"] for msg in messages if msg["role"] == "system"), None)
    gemini_history = []
    for msg in messages:
        if msg["role"] != "system":
            role = "user" if msg["role"] == "user" else "model"
            gemini_history.append({"role": role, "parts": [{"text": msg["content"]}]})
            
    config_args = {}
    if system_instruction:
        config_args["system_instruction"] = system_instruction
    if format == "json":
        config_args["response_mime_type"] = "application/json"
        
    try:
        response = client.models.generate_content(
            model=model_name,
            contents=gemini_history,
            config=types.GenerateContentConfig(**config_args) if config_args else None
        )
    except Exception as e:
        print("GENERATE Error:", type(e))

test_syntax()

def test_stream_syntax():
    model = "gemini-2.5-flash"
    poi_query = "hello"
    try:
        from google import genai
        from google.genai import types
        client = genai.Client(api_key=os.environ.get("GEMINI_API_KEY", ""))
        
        res = client.models.generate_content_stream(
            model=model,
            contents=[{"role": "user", "parts": [{"text": poi_query}]}],
            config=types.GenerateContentConfig(
                system_instruction="testing"
            )
        )
    except Exception as e:
        print("STREAM Error:", type(e))

test_stream_syntax()
print("Syntax OK")
