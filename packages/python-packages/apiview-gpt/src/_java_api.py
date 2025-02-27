from ._gpt_reviewer_openai import GptReviewer

def review_java(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "java")
  return result.json()
