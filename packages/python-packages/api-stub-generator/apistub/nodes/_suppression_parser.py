import re
from typing import List

class SuppressionParser:

    SUPPRESSION_REGEX = re.compile(r'apiview:\s*disable=([a-zA-Z-, ]+)')

    @classmethod
    def parse(cls, comment: str) -> List[str]:
        if not comment:
            return None

        try:
            raw_match = cls.SUPPRESSION_REGEX.findall(comment)[0]
            values = raw_match.replace(" ", "").split(",")
            return values
        except IndexError:
            return None
