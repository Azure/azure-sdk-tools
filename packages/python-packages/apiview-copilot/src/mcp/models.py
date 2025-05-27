from pydantic import BaseModel
from typing import List


class SearchGuidelinesInput(BaseModel):
    query: str


class SearchGuidelinesOutput(BaseModel):
    results: List[str]
