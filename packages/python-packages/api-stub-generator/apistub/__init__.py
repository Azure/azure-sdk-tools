import os
from ._version import VERSION
from ._stub_generator import StubGenerator
from ._token import Token
from ._token_kind import TokenKind
from ._apiview import ApiView, Navigation, NavigationTag, Kind

__version__ = VERSION

__all__ = [
    "StubGenerator",
    "Token",
    "TokenKind",
    "ApiView",
    "Navigation",
    "NavigationTag",
    "Kind",
]


def console_entry_point():
    stub_generator = StubGenerator()
    apiview = stub_generator.generate_tokens()
    json_tokens = stub_generator.serialize(apiview)
    # Write to JSON file
    out_file_path = os.path.join(
        stub_generator.out_path, "{0}_python.json".format(apiview.Name)
    )
    with open(out_file_path, "w") as json_file:
        json_file.write(json_tokens)
