from ._gpt_reviewer_openai import GptReviewer

def review_golang(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "golang")
  return result.json()
