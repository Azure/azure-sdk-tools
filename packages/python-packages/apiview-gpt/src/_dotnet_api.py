from ._gpt_reviewer import GptReviewer

def review_dotnet(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "dotnet")
  return result.json()
