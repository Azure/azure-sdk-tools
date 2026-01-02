# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for defining data models used in APIView Copilot.
"""

from datetime import datetime
from enum import Enum
from typing import Dict, List, Optional, Set

from pydantic import BaseModel, Field, PrivateAttr

from ._sectioned_document import Section


class ExistingComment(BaseModel):
    """
    Represents an existing comment in the APIView.
    This is used to prevent copilot from generating the same comment again.
    """

    line_no: int = Field(description="Line number of the existing comment.", alias="lineNo")
    created_by: str = Field(description="The author of the existing comment.", alias="createdBy")
    comment_text: str = Field(description="The contents of the existing comment.", alias="commentText")
    created_on: datetime = Field(
        description="The datetime the comment was created, in ISO 8601 format (e.g., '2023-10-01T12:00:00Z').",
        alias="createdOn",
    )
    upvotes: Optional[int] = Field(default=0, description="The count of upvotes for the existing comment.")
    downvotes: Optional[int] = Field(default=0, description="The count of downvotes for the existing comment.")
    is_resolved: Optional[bool] = Field(
        default=False, description="Whether the comment is marked resolved.", alias="isResolved"
    )

    class Config:
        """Pydantic configuration for ExistingComment."""

        populate_by_name = True


class APIViewComment(BaseModel):
    """
    Represents a comment in the APIView.
    """

    id: Optional[str] = Field(default=None, description="Unique identifier for the comment.")
    review_id: Optional[str] = Field(
        default=None, description="Unique identifier for the review this comment belongs to.", alias="ReviewId"
    )
    api_revision_id: Optional[str] = Field(
        default=None,
        description="Unique identifier for the API revision this comment belongs to.",
        alias="APIRevisionId",
    )
    element_id: Optional[str] = Field(
        default=None,
        description="Unique identifier for the element this comment belongs to, such as a function or class.",
        alias="ElementId",
    )
    comment_text: Optional[str] = Field(default=None, description="The contents of the comment.", alias="CommentText")
    created_by: Optional[str] = Field(default=None, description="The author of the comment.", alias="CreatedBy")
    created_on: Optional[datetime] = Field(
        default=None,
        description="The datetime the comment was created, in ISO 8601 format (e.g., '2023-10-01T12:00:00Z').",
        alias="CreatedOn",
    )
    is_resolved: Optional[bool] = Field(
        default=False, description="Whether the comment is marked resolved.", alias="IsResolved"
    )
    upvotes: Optional[list[str]] = Field(
        default_factory=list,
        description="List of user IDs who have upvoted the comment.",
        alias="Upvotes",
    )
    downvotes: Optional[list[str]] = Field(
        default_factory=list,
        description="List of user IDs who have down-voted the comment.",
        alias="Downvotes",
    )
    comment_type: Optional[str] = Field(
        default=None,
        description="The type of comment",
        alias="CommentType",
    )
    comment_source: Optional[str] = Field(
        description="The source of the comment: UserGenerated, AIGenerated, or Diagnostic",
        alias="CommentSource",
        default="UserGenerated",
    )
    resolution_locked: Optional[bool] = Field(
        default=False,
        description="Whether the comment resolution is locked and cannot be changed.",
        alias="ResolutionLocked",
    )
    is_deleted: Optional[bool] = Field(
        default=False,
        description="Whether the comment is deleted.",
        alias="IsDeleted",
    )


class Comment(BaseModel):
    """
    Represents a comment in the review result.
    """

    guideline_ids: List[str] = Field(default_factory=list, description="Unique guideline ID or IDs that were violated.")
    memory_ids: List[str] = Field(default_factory=list, description="Unique memory ID or IDs that were referenced.")
    is_generic: bool = Field(
        default=False, description="Whether any portion of the comment was formulated from generic guidance."
    )
    line_no: int = Field(description="Line number of the comment.")
    bad_code: str = Field(
        description="the original code that was bad, cited verbatim. Should contain a single line of code."
    )
    suggestion: Optional[str] = Field(
        description="the suggested code which fixes the bad code. If code is not feasible, a description is fine."
    )
    comment: str = Field(description="the contents of the comment.")
    correlation_id: Optional[str] = Field(
        default=None, description="a correlation ID for grouping similar comments together."
    )
    confidence_score: Optional[float] = Field(
        default=None, description="Confidence score from the judge prompt (0.0 - 1.0)."
    )
    severity: Optional[str] = Field(
        default=None,
        description="The severity level of the comment: 'SUGGESTION', 'SHOULD', 'MUST', or 'QUESTION'.",
    )

    def __init__(self, **data):
        super().__init__(**data)
        if self.suggestion == "" or self.suggestion == "null":
            self.suggestion = None


class Guideline(BaseModel):
    """
    Represents a guideline in CosmosDB.
    """

    id: str = Field(description="Unique identifier for the guideline.")
    title: str = Field(description="Short descriptive title of the guideline.")
    content: str = Field(description="Full text of the guideline.")

    # Classification fields
    language: Optional[str] = Field(
        None,
        # pylint: disable=line-too-long
        description="If this guideline is specific to a language (e.g., 'python'). If omitted, the guideline is language-agnostic.",
    )
    tags: Optional[List[str]] = Field(
        None,
        description="List of tags that classify the guideline.",
    )

    # Relationship fields
    related_guidelines: List[str] = Field(
        default_factory=list, description="List of guideline IDs that are related to this guideline."
    )
    related_examples: List[str] = Field(
        default_factory=list, description="List of example IDs that are related to this guideline."
    )
    related_memories: List[str] = Field(
        default_factory=list, description="List of memory IDs that are related to this guideline."
    )


class ExampleType(str, Enum):
    """Represents the type of example, either 'good' or 'bad'."""

    GOOD = "good"
    BAD = "bad"


class Example(BaseModel):
    """
    Represents an example stored in CosmosDB.
    """

    id: str = Field(description="Unique identifier for the example.")
    title: str = Field(description="Short descriptive title of the example.")
    content: str = Field(description="Code snippet containing the example.")

    # Classification fields
    language: Optional[str] = Field(None, description="If this example is specific to a language (e.g., 'python').")
    service: Optional[str] = Field(
        None,
        description="If this example is specific to a service (e.g., 'azure-functions').",
    )
    is_exception: bool = Field(
        False,
        description="Indicates if this example provides an exception to the guidelines rather than an amplification.",
    )
    tags: Optional[List[str]] = Field(
        None,
        description="List of tags that classify the guideline.",
    )
    example_type: ExampleType = Field(description="Whether this example is 'good' or 'bad'.")

    # Relationship fields
    guideline_ids: List[str] = Field(
        default_factory=list, description="List of guideline IDs to which this example applies."
    )
    memory_ids: List[str] = Field(default_factory=list, description="List of memory IDs to which this example applies.")


class Memory(BaseModel):
    """
    Represents a memory object in CosmosDB.
    """

    id: str = Field(description="Unique identifier for the memory.")
    title: str = Field(description="Short descriptive title of the memory.")
    content: str = Field(description="Content of the memory.")

    # Classification fields
    language: Optional[str] = Field(None, description="If this memory is specific to a language (e.g., 'python').")
    service: Optional[str] = Field(
        None,
        description="If this memory is specific to a service (e.g., 'azure-functions').",
    )
    is_exception: bool = Field(
        False,
        description="Indicates if this memory provides an exception to the guidelines rather than an amplification.",
    )
    source: str = Field(description="The source of the memory, such as 'manual' or 'teams_conversation'.")
    tags: Optional[List[str]] = Field(
        None,
        description="List of tags that classify the memory.",
    )

    # Relationship fields
    related_guidelines: List[str] = Field(
        default_factory=list, description="List of guideline IDs that are related to this memory."
    )
    related_examples: List[str] = Field(
        default_factory=list, description="List of example IDs that are related to this memory."
    )
    related_memories: List[str] = Field(
        default_factory=list, description="List of memory IDs that are related to this memory."
    )


class ReviewResult(BaseModel):
    """Represents the result of a review, containing comments and metadata."""

    comments: List[Comment] = Field(description="List of comments, if any")

    # truly private, never part of Pydantic’s schema or serialization
    _allowed_ids: Set[str] = PrivateAttr(default_factory=set)

    def __init__(
        self,
        *,
        comments: List[Dict] = None,
        allowed_ids: Optional[List[str]] = None,
        section: Optional[Section] = None,
    ):
        comments = comments or []
        # initialize with no comments since we are going to process them
        super().__init__(comments=[])

        # sanitize allowed_ids to convert the search IDs to the proper format
        allowed_ids = [x.replace("=html=", ".html#") for x in allowed_ids] if allowed_ids else None

        # initialize private attr outside of Pydantic’s field system
        object.__setattr__(
            self,
            "_allowed_ids",
            set(allowed_ids) if allowed_ids else set(),
        )
        self._process_comments(comments=comments, section=section)

    def _process_comments(self, *, comments: List[Dict], section: Optional[Section] = None):
        """
        Process comment dictionaries, handling various line_no formats:
        - single number (e.g., "10"): Use as is, cast to int
        - range (e.g., "10-20"): Take the first number
        - list (e.g., "10, 20" or "10, 20-25"): Create a copy of the comment for each line
        - invalid (e.g., "abc"): Use a fallback value of 0
        If section is provided, fix line numbers and only add valid comments.
        """
        result_comments = []
        default_line_no = 0
        for comment in comments:
            raw_line_no = str(comment.get("line_no", "0")).replace(" ", "").strip()

            # Ensure all required fields exist
            if "guideline_ids" not in comment:
                comment["guideline_ids"] = []
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
                    new_comment = Comment(**comment_copy)
                    self._filter_guideline_ids(new_comment)
                    new_comment.line_no = self._find_line_number(section, new_comment)
                    if new_comment.line_no is not None:
                        result_comments.append(new_comment)
                else:
                    try:
                        # Handle single number format (e.g., "10")
                        comment_copy["line_no"] = int(item)
                    except ValueError:
                        # Use fallback value if conversion fails
                        comment_copy["line_no"] = default_line_no
                    new_comment = Comment(**comment_copy)
                    self._filter_guideline_ids(new_comment)
                    new_comment.line_no = self._find_line_number(section, new_comment)
                    if new_comment.line_no is not None:
                        result_comments.append(new_comment)
        self.comments.extend(result_comments)  # pylint: disable=no-member

    def sort(self):
        """
        Sort the comments by line number.
        """
        self.comments.sort(key=lambda x: x.line_no)  # pylint: disable=no-member

    def sorted(self) -> "ReviewResult":
        """
        Return a sorted list of comments.
        """
        self.sort()
        return self

    def _filter_guideline_ids(self, item: Comment):
        """
        Filter out invalid guideline IDs from a comment, keeping only those that resolve.
        """
        if not item.guideline_ids:
            return
        resolved_guideline_ids = set()
        for guideline_id in item.guideline_ids:
            resolved_guideline_id = self._resolve_guideline_id(guideline_id)
            if resolved_guideline_id:
                resolved_guideline_ids.add(resolved_guideline_id)
        item.guideline_ids = list(resolved_guideline_ids)

    def _resolve_guideline_id(self, rid: str) -> str | None:
        """
        Ensure that the guideline ID matches with an actual guideline ID.
        This ensures that the links that appear in APIView should never be broken (404).
        Allows for specific partial matches.
        """
        if rid in self._allowed_ids:
            return rid

        # check if the part of the guideline_id after the # matches the rule_id
        for gid in self._allowed_ids:
            gid_end = gid.split("#")[-1]
            if rid == gid_end:
                return gid
        return None

    def _find_line_number(self, chunk: Optional[Section], comment: Comment) -> Optional[int]:
        """
        Algorithm to correct line numbers that are slightly off. If chunk is None, return the original line_no.
        """
        if chunk is None:
            return comment.line_no
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
        comment.comment = f"{comment.comment} (general comment)"
        return comment.line_no


class CosmosMetricsPeriod(BaseModel):
    """Represents the period information for CosmosDB metrics."""

    start_epoch_s: int
    end_epoch_s: int
    label: str


class CosmosMetricDocument(BaseModel):
    """Pydantic model for CosmosDB metric document."""

    id: str  # "overview|2025-09-13|2025-10-03"
    pk: str  # "overview"  // partition by series, not time
    metric_name: str  # "overview"
    dimensions: Dict[str, str]  # {"language": "python"}
    period: CosmosMetricsPeriod
    label: str  # "2025-09-13_to_2025-10-03"    // or "2025-TW-0"
    values: dict
    updated_at_epoch_s: int  # 1759449600
    source: str = "aggregator@1.0"
