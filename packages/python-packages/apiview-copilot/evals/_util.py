import json


def ensure_json_obj(val):
    """Helper to ensure input is a dict (parsed JSON)."""
    if isinstance(val, str):
        return json.loads(val)
    return val
