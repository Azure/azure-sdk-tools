from ._gpt_reviewer import GptReviewer

def review_typescript(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "typescript")
  return result.json()
