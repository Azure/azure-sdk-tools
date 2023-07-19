from pydantic import BaseModel, Field
from typing import List

class Violation(BaseModel):
    rule_ids: List[str] = Field(description="unique rule ID or IDs that were violated.")
    line_no: int = Field(description="the line number of the violation.")
    bad_code: str = Field(description="the original code that was bad.")
    suggestion: str = Field(description="the suggested fix for the bad code.")
    comment: str = Field(description="a comment about the violation.")

class GuidelinesResult(BaseModel):
    status: str = Field(description="Succeeded if the request completed, or Error if it did not")
    violations: List[Violation] = Field(description="list of violations if any")
