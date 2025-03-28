from .._gpt_reviewer_openai import GptReviewer

def review_cpp(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "cpp")
  return result.model_dump_json()
