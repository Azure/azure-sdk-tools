from ._gpt_reviewer import GptReviewer

def review_go(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "go")
  return result.json()
