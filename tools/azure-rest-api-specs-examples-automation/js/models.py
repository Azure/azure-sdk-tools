import dataclasses
from typing import List


@dataclasses.dataclass(eq=True)
class JsExample:
    target_filename: str
    target_dir: str
    content: str


@dataclasses.dataclass(eq=True)
class JsLintResult:
    succeeded: bool
    examples: List[JsExample]
