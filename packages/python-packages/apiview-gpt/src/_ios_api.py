from ._gpt_reviewer import GptReviewer

def review_ios(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "ios")
  return result.json()
