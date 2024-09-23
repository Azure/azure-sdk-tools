import dataclasses
from typing import List


@dataclasses.dataclass(eq=True)
class GoExample:
    target_filename: str
    target_dir: str
    content: str


@dataclasses.dataclass(eq=True)
class GoVetResult:
    succeeded: bool
    examples: List[GoExample]
