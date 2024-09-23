import dataclasses
from typing import List


@dataclasses.dataclass(eq=True)
class DotNetExample:
    target_filename: str
    target_dir: str
    content: str


@dataclasses.dataclass(eq=True)
class DotNetBuildResult:
    succeeded: bool
    examples: List[DotNetExample]
