from ._gpt_reviewer import GptReviewer

def review_swift(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "swift")
  return result.json()
