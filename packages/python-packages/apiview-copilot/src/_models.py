from enum import Enum
from pydantic import BaseModel, Field, PrivateAttr
from typing import List, Optional, Dict, Set

from ._sectioned_document import Section


class Violation(BaseModel):
    rule_ids: List[str] = Field(
        description="Unique guideline ID or IDs that were violated."
    )
    line_no: int = Field(description="Line number of the violation.")
    bad_code: str = Field(
        description="the original code that was bad, cited verbatim. Should contain a single line of code."
    )
    suggestion: str = Field(
        description="the suggested code which fixes the bad code. If code is not feasible, a description is fine."
    )
    comment: str = Field(description="a comment about the violation.")


class Guideline(BaseModel):
    """
    Represents a guideline in CosmosDB.
    """

    id: str = Field(description="Unique identifier for the guideline.")
    title: str = Field(description="Short descriptive title of the guideline.")
    content: str = Field(description="Full text of the guideline.")
    lang: Optional[str] = Field(
        None,
        description="If this guideline is specific to a language (e.g., 'python'). If omitted, the guideline is language-agnostic.",
    )

    # reverse links
    related_guidelines: Optional[List[str]] = Field(
        description="List of guideline IDs that are related to this guideline."
    )
    related_examples: Optional[List[str]] = Field(
        description="List of example IDs that are related to this guideline."
    )


class ExampleType(str, Enum):
    GOOD = "good"
    BAD = "bad"


class Example(BaseModel):
    """
    Represents an example stored in CosmosDB.
    """

    id: str = Field(description="Unique identifier for the example.")
    guideline_ids: List[str] = Field(
        description="List of guideline IDs to which this example applies."
    )
    content: str = Field(description="Code snippet demonstrating the example.")
    explanation: str = Field(description="Explanation of why this is good/bad.")
    example_type: ExampleType = Field(
        description="Whether this example is 'good' or 'bad'."
    )

    # Classification fields
    lang: Optional[str] = Field(
        None, description="If this example is specific to a language (e.g., 'python')."
    )
    service: Optional[str] = Field(
        None,
        description="If this example is specific to a service (e.g., 'azure-functions').",
    )
    is_exception: bool = Field(
        False,
        description="Indicates if this example provides an exception to the guidelines rather than an amplification.",
    )


class ReviewResult(BaseModel):
    status: str = Field(
        description="Succeeded if the request has no violations. Error if there are violations."
    )
    violations: List[Violation] = Field(description="List of violations if any")

    # truly private, never part of Pydantic’s schema or serialization
    _guideline_ids: Set[str] = PrivateAttr(default_factory=set)

    def __init__(
        self,
        *,
        guideline_ids: Optional[List[str]] = None,
        **data,
    ):
        actual_violations = data.pop("violations", [])
        data["violations"] = []
        super().__init__(**data)
        # initialize private attr outside of Pydantic’s field system
        object.__setattr__(
            self,
            "_guideline_ids",
            set(guideline_ids) if guideline_ids else set(),
        )
        self._process_violations(actual_violations)

    def _process_violations(self, violations: List[Dict]):
        """
        Process violation dictionaries, handling various line_no formats:
        - single number (e.g., "10"): Use as is, cast to int
        - range (e.g., "10-20"): Take the first number
        - list (e.g., "10, 20" or "10, 20-25"): Create a copy of the violation for each line
        - invalid (e.g., "abc"): Use a fallback value of 0
        """
        result_violations = []
        default_line_no = 0
        for violation in violations:
            raw_line_no = str(violation.get("line_no", "0")).replace(" ", "").strip()
            # if violation doesn't have suggestion, set it to empty string
            if "suggestion" not in violation:
                violation["suggestion"] = ""

            # handle common line number issues from the LLM
            for item in raw_line_no.split(","):
                item = item.strip()
                violation_copy = (
                    violation.copy()
                )  # Create a copy of the violation dictionary
                if "-" in item:
                    # Handle range format (e.g., "10-20")
                    first_num = item.split("-")[0].strip()
                    try:
                        violation_copy["line_no"] = int(first_num)
                    except ValueError:
                        # Use fallback value if conversion fails
                        violation_copy["line_no"] = default_line_no
                    result_violations.append(Violation(**violation_copy))
                else:
                    try:
                        # Handle single number format (e.g., "10")
                        violation_copy["line_no"] = int(item)
                    except ValueError:
                        # Use fallback value if conversion fails
                        violation_copy["line_no"] = default_line_no
                    result_violations.append(Violation(**violation_copy))
        self.violations.extend(result_violations)

    def merge(self, other: "ReviewResult", *, section: Section):
        """
        Merge two ReviewResult objects.
        """
        self._guideline_ids.update(other._guideline_ids)
        self._merge_violations(other.violations, section)
        if len(self.violations) > 0:
            self.status = "Error"

    def _validate(self, item: Violation) -> bool:
        """
        Validates the Violation object.
        If the result of validation is that the violation is invalid, return False.
        Even if the violation is changed during validation, if it is still valid, return True.
        """
        # Validate and sanitize rule IDs
        resolved_rule_ids = set()
        for rule_id in item.rule_ids:
            resolved_rule_id = self._resolve_rule_id(rule_id)
            if resolved_rule_id:
                resolved_rule_ids.add(resolved_rule_id)
        if not resolved_rule_ids:
            return False
        else:
            item.rule_ids = list(resolved_rule_ids)
        return True

    def _resolve_rule_id(self, rid: str) -> str | None:
        """
        Ensure that the rule ID matches with an actual guideline ID.
        This ensures that the links that appear in APIView should never be broken (404).
        Allows for specific partial matches.
        """
        if rid in self._guideline_ids:
            return rid

        # check if the part of the guideline_id after the # matches the rule_id
        for gid in self._guideline_ids:
            gid_end = gid.split("#")[-1]
            if rid == gid_end:
                return gid
        print(f"WARNING: Rule ID {rid} not found. Possible hallucination.")
        return None

    def _merge_violations(self, violations: List[Violation], section: Section):
        """
        Process and combine batches of violations as needed. Attempts to
        determine line numbers and ignores violations that can't be mapped to a line.
        If multiple of the same violation are found on the same line, they are combined.
        """
        if not violations:
            return

        combined_violations = {}
        for violation in violations:
            # Cure minor deviations in line numbers. If the line number can't be resolved, skip
            line_no = self._find_line_number(section, violation)
            if line_no is None:
                continue
            violation.line_no = line_no
            existing = combined_violations.get(line_no, None)
            if existing:
                for rule_id in violation.rule_ids:
                    if rule_id not in existing.rule_ids:
                        existing.rule_ids.append(rule_id)
                        if existing.suggestion != violation.suggestion:
                            # FIXME: Collect all suggestions and use the most popular??
                            existing.suggestion = violation.suggestion
                        existing.comment = existing.comment + " " + violation.comment
            else:
                combined_violations[line_no] = violation

        # remove any violations that don't pass validation and then add them to the list
        filtered_violations = [
            x for x in combined_violations.values() if self._validate(x)
        ]
        self.violations.extend(filtered_violations)

    def _find_line_number(self, chunk: Section, violation: Violation) -> Optional[int]:
        """
        Algorithm to correct line numbers that are slightly off.
        """
        bad_code = violation.bad_code
        target_idx = violation.line_no - chunk.start_line_no - 1
        try:
            if chunk.lines[target_idx].strip() == bad_code.strip():
                return violation.line_no
        except IndexError:
            pass
        # Search up until the start of the chunk or an empty line is reached for a match
        for i in range(target_idx - 1, -1, -1):
            if chunk.lines[i].strip().startswith(bad_code.strip()):
                updated_idx = chunk.start_line_no + i + 1
                return updated_idx
            if not chunk.lines[i].strip():
                break

        # If that doesn't work, search down until the end of the chunk or an empty line is reached for a match
        for i in range(target_idx + 1, len(chunk.lines)):
            if chunk.lines[i].strip().startswith(bad_code.strip()):
                updated_idx = chunk.start_line_no + i + 1
                return updated_idx
            if not chunk.lines[i].strip():
                break

        # If no match is found, return None
        print(
            f"WARNING: Could not find match for code '{violation.bad_code}' at or near line {violation.line_no}"
        )
        return None

    def sort(self):
        """
        Sort the violations by line number.
        """
        self.violations.sort(key=lambda x: x.line_no)
