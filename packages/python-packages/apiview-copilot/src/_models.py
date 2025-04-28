from enum import Enum
from pydantic import BaseModel, Field, PrivateAttr
from typing import List, Optional, Dict, Set, Union

from ._sectioned_document import Section


class Comment(BaseModel):
    """
    Represents a comment in the review result.
    """

    rule_ids: List[str] = Field(description="Unique guideline ID or IDs that were violated.")
    line_no: int = Field(description="Line number of the comment.")
    bad_code: str = Field(
        description="the original code that was bad, cited verbatim. Should contain a single line of code."
    )
    suggestion: str = Field(
        description="the suggested code which fixes the bad code. If code is not feasible, a description is fine."
    )
    comment: str = Field(description="the contents of the comment.")
    source: str = Field(description="unique tag for the prompt that produced the comment.")


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
    related_examples: Optional[List[str]] = Field(description="List of example IDs that are related to this guideline.")


class ExampleType(str, Enum):
    GOOD = "good"
    BAD = "bad"


class Example(BaseModel):
    """
    Represents an example stored in CosmosDB.
    """

    id: str = Field(description="Unique identifier for the example.")
    guideline_ids: List[str] = Field(description="List of guideline IDs to which this example applies.")
    content: str = Field(description="Code snippet demonstrating the example.")
    explanation: str = Field(description="Explanation of why this is good/bad.")
    example_type: ExampleType = Field(description="Whether this example is 'good' or 'bad'.")

    # Classification fields
    lang: Optional[str] = Field(None, description="If this example is specific to a language (e.g., 'python').")
    service: Optional[str] = Field(
        None,
        description="If this example is specific to a service (e.g., 'azure-functions').",
    )
    is_exception: bool = Field(
        False,
        description="Indicates if this example provides an exception to the guidelines rather than an amplification.",
    )


class ReviewResult(BaseModel):
    comments: List[Comment] = Field(description="List of comments, if any")

    # truly private, never part of Pydantic’s schema or serialization
    _guideline_ids: Set[str] = PrivateAttr(default_factory=set)

    def __init__(
        self,
        *,
        guideline_ids: Optional[List[str]] = None,
        **data,
    ):
        comments = data.pop("comments", [])
        data["comments"] = []
        super().__init__(**data)
        # initialize private attr outside of Pydantic’s field system
        object.__setattr__(
            self,
            "_guideline_ids",
            set(guideline_ids) if guideline_ids else set(),
        )
        self._process_comments(comments)

    def _process_comments(self, comments: List[Dict]):
        """
        Process comment dictionaries, handling various line_no formats:
        - single number (e.g., "10"): Use as is, cast to int
        - range (e.g., "10-20"): Take the first number
        - list (e.g., "10, 20" or "10, 20-25"): Create a copy of the comment for each line
        - invalid (e.g., "abc"): Use a fallback value of 0
        """
        result_comments = []
        default_line_no = 0
        for comment in comments:
            raw_line_no = str(comment.get("line_no", "0")).replace(" ", "").strip()
            bad_code = comment.get("bad_code", None)
            code_fix = comment.get("code_fix", None)
            if code_fix == bad_code:
                code_fix = None
            general_suggestion = comment.get("suggestion", None)
            comment_val = comment.get("comment", None)

            # Ensure all required fields exist
            comment["suggestion"] = code_fix or ""
            if general_suggestion and general_suggestion != comment_val:
                comment["comment"] = f"{comment_val}. Suggest: {general_suggestion}"
            if "rule_ids" not in comment:
                comment["rule_ids"] = []
            if "source" not in comment:
                comment["source"] = "unknown"

            # handle common line number issues from the LLM
            for item in raw_line_no.split(","):
                item = item.strip()
                comment_copy = comment.copy()  # Create a copy of the comment dictionary
                if "-" in item:
                    # Handle range format (e.g., "10-20")
                    first_num = item.split("-")[0].strip()
                    try:
                        comment_copy["line_no"] = int(first_num)
                    except ValueError:
                        # Use fallback value if conversion fails
                        comment_copy["line_no"] = default_line_no
                    result_comments.append(Comment(**comment_copy))
                else:
                    try:
                        # Handle single number format (e.g., "10")
                        comment_copy["line_no"] = int(item)
                    except ValueError:
                        # Use fallback value if conversion fails
                        comment_copy["line_no"] = default_line_no
                    result_comments.append(Comment(**comment_copy))
        self.comments.extend(result_comments)
        self._deduplicate_comments()
        self.sort()

    def _deduplicate_comments(self):
        """
        Deduplicate comments based on line number and rule IDs.
        """
        seen = {}
        for comment in self.comments:
            key = comment.line_no
            if key not in seen:
                seen[key] = comment
            else:
                # Prefer a comment with rule IDs over ones without
                if seen[key].rule_ids:
                    continue
                seen[key] = comment
        self.comments = list(seen.values())

    def merge(self, other: "ReviewResult", *, section: Section):
        """
        Merge two ReviewResult objects.
        """
        self._guideline_ids.update(other._guideline_ids)
        self._merge_comments(other.comments, section)

    def _validate(self, item: Comment) -> bool:
        """
        Validates the Improvement object.
        If the result of validation is that the comment is invalid, return False.
        Even if the comment is changed during validation, if it is still valid, return True.
        """
        # If the rule IDs are empty, assume valid. These come from the
        # general prompts as opposed to the guideline-specific ones.
        if not item.rule_ids:
            return True

        # Validate and sanitize rule IDs, if provided
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

    def _merge_comments(self, comments: List[Comment], section: Section):
        """
        Process and combine batches of comments as needed. Attempts to
        determine line numbers and ignores comments that can't be mapped to a line.
        If multiple of the same comment are found on the same line, they are combined.
        """
        if not comments:
            return

        combined_comments = {}
        for comment in comments:
            # Cure minor deviations in line numbers. If the line number can't be resolved, skip
            line_no = self._find_line_number(section, comment)
            if line_no is None:
                continue
            comment.line_no = line_no
            existing = combined_comments.get(line_no, None)
            if existing:
                for rule_id in comment.rule_ids:
                    if rule_id not in existing.rule_ids:
                        existing.rule_ids.append(rule_id)
                        if existing.suggestion != comment.suggestion:
                            # FIXME: Collect all suggestions and use the most popular??
                            existing.suggestion = comment.suggestion
                        existing.comment = existing.comment + " " + comment.comment
            else:
                combined_comments[line_no] = comment

        # remove any comments that don't pass validation and then add them to the list
        filtered_comments = [x for x in combined_comments.values() if self._validate(x)]
        self.comments.extend(filtered_comments)

    def _find_line_number(self, chunk: Section, comment: Comment) -> Optional[int]:
        """
        Algorithm to correct line numbers that are slightly off.
        """
        bad_code = comment.bad_code
        target_idx = chunk.idx_for_line_no(comment.line_no)
        if target_idx is None:
            print(f"WARNING: Could not find line number {comment.line_no} in chunk.")
            return comment.line_no
        try:
            left = chunk.lines[target_idx].line.strip()
            right = bad_code.strip()
            if left == right:
                return comment.line_no
            elif left.startswith(right):
                # If the left side starts with the right side, return the line number
                return comment.line_no
            elif right.startswith(left):
                # If the right side starts with the left side, return the line number
                return comment.line_no
        except IndexError:
            pass

        # Search up until the start of the chunk or an empty line is reached for a match
        for i in range(target_idx - 1, -1, -1):
            left = chunk.lines[i].line.strip()
            if not left:
                break
            if left.startswith(right) or right.startswith(left):
                updated_no = chunk.lines[i].line_no
                return updated_no

        # If that doesn't work, search down until the end of the chunk or an empty line is reached for a match
        for i in range(target_idx + 1, len(chunk.lines)):
            left = chunk.lines[i].line.strip()
            if not left:
                break
            if left.startswith(right) or right.startswith(left):
                updated_no = chunk.lines[i].line_no
                return updated_no

        # If no match is found, return the original line number
        print(f"WARNING: Could not find match for code '{comment.bad_code}' at or near line {comment.line_no}")
        comment.comment = f"${comment.comment} (general comment)"
        return comment.line_no

    def sort(self):
        """
        Sort the comments by line number.
        """
        self.comments.sort(key=lambda x: x.line_no)
