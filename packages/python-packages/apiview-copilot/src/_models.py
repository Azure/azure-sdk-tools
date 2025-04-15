from datetime import datetime
from pydantic import BaseModel, Field
from typing import List, Optional, Union

from ._sectioned_document import Section


class Violation(BaseModel):
    rule_ids: List[str] = Field(description="unique rule ID or IDs that were violated.")
    line_no: Optional[int] = Field(description="the line number of the violation.")
    bad_code: str = Field(
        description="the original code that was bad, cited verbatim. Should contain a single line of code."
    )
    suggestion: str = Field(
        description="the suggested code which fixes the bad code. If code is not feasible, a description is fine."
    )
    comment: str = Field(description="a comment about the violation.")


class GuidelinesResult(BaseModel):
    status: str = Field(
        description="Succeeded if the request has no violations. Error if there are violations."
    )
    violations: List[Violation] = Field(description="list of violations if any")

    def merge(self, other: "GuidelinesResult", *, section: Section):
        """
        Merge two GuidelinesResult objects.
        """
        self.violations.extend(self._process_violations(other.violations, section))
        if len(self.violations) > 0:
            self.status = "Error"

    def validate(self, *, guidelines: List[dict]):
        """
        Runs validations on the GuidelinesResult collection.
        For now this is just ensure rule IDs are valid.
        """
        self._process_rule_ids(guidelines)

    def _process_rule_ids(self, guidelines):
        """
        Ensure that each rule ID matches with an actual guideline ID.
        This ensures that the links that appear in APIView should never be broken (404).
        """
        # create an index for easy lookup
        index = {x["id"]: x for x in guidelines}
        for violation in self.violations:
            to_remove = []
            to_add = []
            for rule_id in violation.rule_ids:
                try:
                    index[rule_id]
                    continue
                except KeyError:
                    # see if any guideline ID ends with the rule_id. If so, update it and preserve in the index
                    matched = False
                    for guideline in guidelines:
                        if guideline["id"].endswith(rule_id):
                            to_remove.append(rule_id)
                            to_add.append(guideline["id"])
                            index[rule_id] = guideline["id"]
                            matched = True
                            break
                    if matched:
                        continue
                    # no match or partial match found, so remove the rule_id
                    to_remove.append(rule_id)
                    print(
                        f"WARNING: Rule ID {rule_id} not found. Possible hallucination."
                    )
            # update the rule_ids arrays with the new values. Don't modify the array while iterating over it!
            for rule_id in to_remove:
                violation.rule_ids.remove(rule_id)
            for rule_id in to_add:
                violation.rule_ids.append(rule_id)

    def _process_violations(
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
            # TODO: Temporarily disabling this
            # line_no = self._find_line_number(section, violation.bad_code)
            # violation.line_no = line_no
            # FIXME see: https://github.com/Azure/azure-sdk-tools/issues/6590
            # if not line_no:
            #     continue
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


class VectorDocument(BaseModel):
    id: Optional[str] = Field(description="unique ID of the document")
    language: str = Field(description="programming language of the document")
    bad_code: str = Field(description="the bad coding pattern", alias="badCode")
    good_code: Optional[str] = Field(
        description="the suggested fix for the bad code", alias="goodCode"
    )
    comment: Optional[str] = Field(description="a comment about the violation")
    guideline_ids: Optional[List[str]] = Field(
        description="list of guideline IDs that apply to this document",
        alias="guidelineIds",
    )


class VectorSearchResult(BaseModel):
    confidence: float = Field(description="confidence score of the match")
    document: VectorDocument = Field(description="the matching document")
