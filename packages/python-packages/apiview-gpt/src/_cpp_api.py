from ._gpt_reviewer import GptReviewer

def review_cpp(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "cpp")
  return result.json()
