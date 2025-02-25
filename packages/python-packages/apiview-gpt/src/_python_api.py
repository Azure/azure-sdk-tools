from ._gpt_reviewer_openai import GptReviewer

def review_python(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "python")
  return result.json()
