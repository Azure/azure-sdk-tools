# Python Maintainability

Review how easily someone can read, change, and test the code, not just whether
it runs. Report concrete problems, not personal style preferences.

## What to Check

- **Clear types:** Keep flexible JSON at external boundaries. If helpers pass
  records with known fields, consider a `TypedDict` or dataclass. A return such
  as `tuple[list[dict[str, Any]], dict[str, int]]` can hide field names and the
  meaning of each result. Suggest a named result when callers need that clarity;
  simple tuples and dictionaries are still fine.
- **Changes to shared data:** Check whether several helpers change the same
  dictionary, list, or counters. If understanding the result requires tracing
  every helper, consider returning selected data and building the final result
  in one place. Preserve validation and output-size limits.
- **Focused helpers:** Look for functions that mix fetching, parsing, selecting,
  and formatting data, or repeatedly branch between two formats. Suggest small
  helpers where they make each path easier to follow, not a new framework.
- **Honest names:** Make names match what helpers return. A parser that returns
  zero or one value may be clearer as `parse_*` returning a value or `None`.
  Consider `yield` when callers only iterate; do not require generators everywhere.
- **Useful tests:** Prefer checks of results and behavior over exact source code
  or prompt sentences. Keep essential structural and safety checks. Use example
  scenarios to test agent behavior; wording assertions alone do not prove it.

## How to Comment

Comment only on problems introduced or worsened by the change. Point to a changed
line, explain what makes the code harder to maintain, and suggest the smallest
useful change. Do not invent a runtime failure or assign bug severity to a design
suggestion.

Start with **Maintainability feedback:**. Use short, everyday sentences, such as
"Could these helpers return their results instead of changing the same dictionary?"
Avoid jargon and long explanations.

Read existing review comments first. Skip a concern another reviewer already
raised, even if you would phrase it differently. Prefer a few useful comments
over a checklist of optional refactors.
