from pydantic import BaseModel, Field
from typing import List, Optional

class Violation(BaseModel):
    rule_ids: List[str] = Field(description="unique rule ID or IDs that were violated.")
    line_no: Optional[int] = Field(description="the line number of the violation.")
    bad_code: str = Field(description="the original code that was bad, cited verbatim.")
    suggestion: str = Field(description="the suggested fix for the bad code.")
    comment: str = Field(description="a comment about the violation.")

class GuidelinesResult(BaseModel):
    status: str = Field(description="Succeeded if the request has no violations. Error if there are violations.")
    violations: List[Violation] = Field(description="list of violations if any")
