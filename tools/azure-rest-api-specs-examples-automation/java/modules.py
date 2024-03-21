import dataclasses
from typing import List


@dataclasses.dataclass(eq=True)
class JavaExample:
    target_filename: str
    target_dir: str
    content: str


@dataclasses.dataclass(eq=True)
class JavaFormatResult:
    succeeded: bool
    examples: List[JavaExample]
