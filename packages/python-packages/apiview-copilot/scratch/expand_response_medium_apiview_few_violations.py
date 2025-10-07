import json

# Read the file
with open(r"c:\\repos\\azure-sdk-tools\\packages\\python-packages\\apiview-copilot\\evals\\tests\\apiview_legacy\\medium_apiview_few_violations.json", "r", encoding="utf-8") as f:
    data = json.load(f)

# Expand the response field
if isinstance(data.get("response"), str):
    data["response"] = json.loads(data["response"])

# Write back the file
with open(r"c:\\repos\\azure-sdk-tools\\packages\\python-packages\\apiview-copilot\\evals\\tests\\apiview_legacy\\medium_apiview_few_violations.json", "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2, ensure_ascii=False)
