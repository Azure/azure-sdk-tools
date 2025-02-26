from ._gpt_reviewer_openai import GptReviewer

def review_ios(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "ios")
  return result.json()
