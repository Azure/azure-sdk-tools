from ._gpt_reviewer import GptReviewer

def review_rest(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "rest")
  return result.json()
