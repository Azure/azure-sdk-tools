from ._kernel import create_kernel
from typing import Optional
from semantic_kernel.planners.function_calling_stepwise_planner import FunctionCallingPlanner


class AgentReviewPlanner(FunctionCallingPlanner):
    def __init__(
        self,
        *,
        target: str,
        base: Optional[str] = None,
        language: str,
        comments: Optional[str] = None,
        outline: Optional[str] = None,
    ):
        self.kernel = create_kernel()
        self.target = target
        self.base = base
        self.language = language
        self.comments = comments
        self.outline = outline
        self.goal = """
        Review the provided API code for SDK guidelines issues.
        Chunk the code, run guideline and language reviews per chunk (using retrieved guideline evidence),
        summarize all changes, unify redundant comments, and filter out weak findings in two passes.
        Return only strong, actionable feedback with supporting links.
        """

    def create_plan(self, goal):
        # Placeholder: In real usage, use SK's planner or prompt
        return goal

    def run(self):
        context = {
            "target": self.target,
            "base": self.base,
            "language": self.language,
            "comments": self.comments,
            "outline": self.outline,
        }
        plan = self.create_plan(self.goal)
        result = self.kernel.run(plan, context)
        return result
