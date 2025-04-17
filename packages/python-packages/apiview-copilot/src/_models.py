from enum import Enum
from pydantic import BaseModel, Field
from typing import List, Optional, Union, Dict

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
    violations: List[Violation] = Field(description="list of violations if any")

    def __init__(self, **data):
        actual_violations = data.pop("violations", [])
        data["violations"] = []
        super().__init__(**data)
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
        self.violations.extend(self._merge_violations(other.violations, section))
        if len(self.violations) > 0:
            self.status = "Error"

    def validate(self, *, guideline_ids: List[str]):
        """
        Runs validations on the ReviewResult collection.
        For now this is just ensure rule IDs are valid.
        """
        # TODO: Disable for now. See: https://github.com/Azure/azure-sdk-tools/issues/10303
        # self._process_rule_ids(guideline_ids)

    def _process_rule_ids(self, guideline_ids: List[str]):
        """
        Ensure that each rule ID matches with an actual guideline ID.
        This ensures that the links that appear in APIView should never be broken (404).
        """
        return
        # FIXME: Fix up this logic...
        # create an index for easy lookup
        # index = set(guideline_ids)
        # for violation in self.violations:
        #     to_remove = []
        #     to_add = []
        #     for rule_id in violation.rule_ids:
        #         if rule_id in index:
        #             # see if any guideline ID ends with the rule_id. If so, update it and preserve in the index
        #             matched = False
        #             for gid in guideline_ids:
        #                 if gid.endswith(rule_id):
        #                     to_remove.append(rule_id)
        #                     to_add.append(gid)
        #                     index[rule_id] = gid
        #                     matched = True
        #                     break
        #             if matched:
        #                 continue
        #             # no match or partial match found, so remove the rule_id
        #             to_remove.append(rule_id)
        #             print(
        #                 f"WARNING: Rule ID {rule_id} not found. Possible hallucination."
        #             )
        #     # update the rule_ids arrays with the new values. Don't modify the array while iterating over it!
        #     for rule_id in to_remove:
        #         violation.rule_ids.remove(rule_id)
        #     for rule_id in to_add:
        #         violation.rule_ids.append(rule_id)

    def _merge_violations(
        self, violations: List[Violation], section: Section
    ) -> List[Violation]:
        """
        Process and combine batches of violations as needed. Attempts to
        determine line numbers and ignores violations that can't be mapped to a line.
        If multiple of the same violation are found on the same line, they are combined.
        """
        if not violations:
            return violations

        combined_violations = {}
        for violation in violations:
            # TODO: Re-visit this logic if line numbers from the GPT are wrong.
            # line_no = self._find_line_number(section, violation.bad_code)
            # violation.line_no = line_no
            line_no = violation.line_no
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
        # this logic tosses out violations that can't be mapped to an actual line
        return [x for x in combined_violations.values() if x.line_no != 1]

    def _find_line_number(self, chunk: Section, bad_code: str) -> Union[int, None]:
        """
        Find the line number of the bad code in the chunk.
        This is a bit of a hack, but it works for now.
        """
        offset = chunk.start_line_no
        line_no = None
        for i, line in enumerate(chunk.lines):
            # FIXME: see: https://github.com/Azure/azure-sdk-tools/issues/6572
            if line.strip().startswith(bad_code.strip()):
                if line_no is None:
                    line_no = offset + i
                else:
                    print(
                        f"WARNING: Found multiple instances of bad code, default to first: {bad_code}"
                    )
        # FIXME: see: https://github.com/Azure/azure-sdk-tools/issues/6572
        if not line_no:
            print(
                f"WARNING: Could not find bad code. Trying less precise method: {bad_code}"
            )
            for i, line in enumerate(chunk.lines):
                if bad_code.strip().startswith(line.strip()):
                    if line_no is None:
                        line_no = offset + i
                    else:
                        print(
                            f"WARNING: Found multiple instances of bad code, default to first: {bad_code}"
                        )
        return line_no

    def sort(self):
        """
        Sort the violations by line number.
        """
        self.violations.sort(key=lambda x: x.line_no)
