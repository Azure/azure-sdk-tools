
import json

class CustomAPIViewEvaluator:

    def __init__(self):
        pass

    def __call__(self, *, response: str, query: str, language: str, output: str, **kwargs):
        expected = json.loads(response)
        actual = json.loads(output)

        # TODO - add more metrics for known/determininstic things like which violations are missing,
        # whether a line number is wrong, missing rule_id, wrong bad_code, etc
        review_eval = {
            "violations_found": len(actual["violations"]),
            "total_violations": len(expected["violations"]),
            "percent_coverage": len(actual["violations"]) / len(expected["violations"]) * 100,
        }
        return review_eval


def review_apiview(query: str, language: str):
    from src._gpt_reviewer_openai import GptReviewer
    rg = GptReviewer()
    review = rg.get_response(query, language)
    return {"response": review.model_dump_json()}
