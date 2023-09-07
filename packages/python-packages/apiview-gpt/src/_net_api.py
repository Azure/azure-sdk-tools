from ._gpt_reviewer import GptReviewer

def review_net(code):
  reviewer = GptReviewer()
  result = reviewer.get_response(code, "c#")
  return result.json()
