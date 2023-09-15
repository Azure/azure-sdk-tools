from ._gpt_reviewer import GptReviewer

def review_android(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "android")
  return result.json()
