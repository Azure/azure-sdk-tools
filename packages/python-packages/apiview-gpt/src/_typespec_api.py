from ._gpt_reviewer import GptReviewer

def review_typespec(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "typespec")
  return result.json()
