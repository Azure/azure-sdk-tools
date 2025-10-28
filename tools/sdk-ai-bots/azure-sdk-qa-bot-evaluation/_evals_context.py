from typing import Optional


class EvalateContext:
    def __init__(self, ai_project_endpoint: Optional[str] = None):
        self._ai_project_endpoint = ai_project_endpoint
        
