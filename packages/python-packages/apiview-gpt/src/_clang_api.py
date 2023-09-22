from ._gpt_reviewer import GptReviewer

def review_clang(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "clang")
  return result.json()
