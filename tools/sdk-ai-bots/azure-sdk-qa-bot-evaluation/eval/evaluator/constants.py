QA_BOT_EVALS_WEIGHT: dict[str, float] = {
    "similarity_weight": 0.6,  # Similarity between expected and actual
    "response_completeness_weight": 0.4,  # Response completeness evaluation
    # "reference_weight": 0.2  # Reference URL matching weight
}
EVALUATION_PASS_FAIL_MAPPING = {
    True: "pass",
    False: "fail",
}