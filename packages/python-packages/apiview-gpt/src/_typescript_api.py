from ._gpt_reviewer_openai import GptReviewer

def review_typescript(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "typescript")
  return result.json()
