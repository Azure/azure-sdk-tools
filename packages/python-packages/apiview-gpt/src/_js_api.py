from ._gpt_reviewer import GptReviewer

def review_js(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "js")
  return result.json()
