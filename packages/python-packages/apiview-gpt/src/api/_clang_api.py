from .._gpt_reviewer_openai import GptReviewer

def review_clang(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "clang")
  return result.model_dump_json()
