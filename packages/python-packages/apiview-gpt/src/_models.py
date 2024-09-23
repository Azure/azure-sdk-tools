from datetime import datetime
from pydantic import BaseModel, Field
from typing import List, Optional

class Violation(BaseModel):
    rule_ids: List[str] = Field(description="unique rule ID or IDs that were violated.")
    line_no: Optional[int] = Field(description="the line number of the violation.")
    bad_code: str = Field(description="the original code that was bad, cited verbatim.")
    suggestion: str = Field(description="the suggested code which fixes the bad code. If code is not feasible, a description is fine.")
    comment: str = Field(description="a comment about the violation.")

class GuidelinesResult(BaseModel):
    status: str = Field(description="Succeeded if the request has no violations. Error if there are violations.")
    violations: List[Violation] = Field(description="list of violations if any")

class VectorDocument(BaseModel):
    id: Optional[str] = Field(description="unique ID of the document")
    language: str = Field(description="programming language of the document")
    bad_code: str = Field(description="the bad coding pattern", alias="badCode")
    good_code: Optional[str] = Field(description="the suggested fix for the bad code", alias="goodCode")
    comment: Optional[str] = Field(description="a comment about the violation")
    guideline_ids: Optional[List[str]] = Field(description="list of guideline IDs that apply to this document", alias="guidelineIds")

class VectorSearchResult(BaseModel):
    confidence: float = Field(description="confidence score of the match")
    document: VectorDocument = Field(description="the matching document")
