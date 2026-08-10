"""Evaluation criteria for the QA bot.

``criteria`` builds ``TestingCriterionAzureAIEvaluator`` lists for the builtin
LLM evaluators (similarity, response_completeness, groundedness).
"""

from . import criteria

__all__ = ["criteria"]
