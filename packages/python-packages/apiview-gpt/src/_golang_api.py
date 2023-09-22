from ._gpt_reviewer import GptReviewer

def review_golang(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "golang")
  return result.json()
