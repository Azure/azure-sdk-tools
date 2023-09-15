from ._gpt_reviewer import GptReviewer

def review_python(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "python")
  return result.json()
