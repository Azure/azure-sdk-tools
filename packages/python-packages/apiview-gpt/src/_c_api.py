from ._gpt_reviewer import GptReviewer

def review_c(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "c")
  return result.json()
