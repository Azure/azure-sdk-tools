from ._version import VERSION
from ._stub_generator import StubGenerator
from ._token import Token
from ._token_kind import TokenKind
from ._apiview import ApiView, Navigation, NavigationTag,Kind

__version__ = VERSION

__all__ = [
    "StubGenerator",
    "Token",
    "TokenKind",
    "ApiView",
    "Navigation",
    "NavigationTag",
    "Kind"
    ]


def console_entry_point():
    stub_generator = StubGenerator()
    stub_generator.generate_tokens()
