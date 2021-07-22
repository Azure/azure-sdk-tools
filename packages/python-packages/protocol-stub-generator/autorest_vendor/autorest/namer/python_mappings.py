# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from enum import Enum

basic_latin_chars = {
    " ": "Space",
    "!": "ExclamationMark",
    '"': "QuotationMark",
    "#": "NumberSign",
    "$": "DollarSign",
    "%": "PercentSign",
    "&": "Ampersand",
    "'": "Apostrophe",
    "(": "LeftParenthesis",
    ")": "RightParenthesis",
    "*": "Asterisk",
    "+": "PlusSign",
    ",": "Comma",
    "-": "HyphenMinus",
    ".": "FullStop",
    "/": "Slash",
    "0": "Zero",
    "1": "One",
    "2": "Two",
    "3": "Three",
    "4": "Four",
    "5": "Five",
    "6": "Six",
    "7": "Seven",
    "8": "Eight",
    "9": "Nine",
    ":": "Colon",
    ";": "Semicolon",
    "<": "LessThanSign",
    "=": "EqualSign",
    ">": "GreaterThanSign",
    "?": "QuestionMark",
    "@": "AtSign",
    "[": "LeftSquareBracket",
    "\\": "Backslash",
    "]": "RightSquareBracket",
    "^": "CircumflexAccent",
    "`": "GraveAccent",
    "{": "LeftCurlyBracket",
    "|": "VerticalBar",
    "}": "RightCurlyBracket",
    "~": "Tilde",
}

class PadType(Enum):
    Model = "Model"
    Method = "Method"
    Parameter = "Parameter"
    Enum = "Enum"
    Property = "Property"

_always_reserved = [
    "and",
    "as",
    "assert",
    "break",
    "class",
    "continue",
    "def",
    "del",
    "elif",
    "else",
    "except",
    "exec",
    "finally",
    "for",
    "from",
    "global",
    "if",
    "import",
    "in",
    "is",
    "lambda",
    "not",
    "or",
    "pass",
    "raise",
    "return",
    "try",
    "while",
    "with",
    "yield",
    "async",
    "await"
]

reserved_words = {
    PadType.Method: [
        *_always_reserved
    ],
    PadType.Parameter: [
        "self",
        # these are kwargs we've reserved for our autorest generated operations
        "content_type",
        "accept",
        "cls",
        "polling",
        "continuation_token",  # for LRO calls
        # these are transport kwargs
        # https://github.com/Azure/azure-sdk-for-python/blob/master/sdk/core/azure-core/CLIENT_LIBRARY_DEVELOPER.md#transport
        "connection_timeout",
        "connection_verify",
        "connection_cert",
        "connection_data_block_size",
        "use_env_settings",
        # the following aren't in the readme, but Xiang said these are also transport kwargs
        "read_timeout",
        "proxies",
        "cookies",
        # these are policy kwargs
        # https://github.com/Azure/azure-sdk-for-python/blob/master/sdk/core/azure-core/CLIENT_LIBRARY_DEVELOPER.md#available-policies
        "base_headers",
        "headers",
        "request_id",
        "auto_request_id",
        "base_user_agent",
        "user_agent",
        "user_agent_overwrite",
        "user_agent_use_env",
        "user_agent",
        "sdk_moniker",
        "logging_enable",
        "logger",
        "response_encoding",
        "proxies",
        "raw_request_hook",
        "raw_response_hook",
        "network_span_namer",
        "tracing_attributes",
        "permit_redirects",
        "redirect_max",
        "redirect_remove_headers",
        "redirect_on_status_codes",
        "permit_redirects",
        "redirect_max",
        "redirect_remove_headers",
        "redirect_on_status_codes",
        "retry_total",
        "retry_connect",
        "retry_read",
        "retry_status",
        "retry_backoff_factor",
        "retry_backoff_max",
        "retry_mode",
        "retry_on_status_codes",
        "retry_total",
        "retry_connect",
        "retry_read",
        "retry_status",
        "retry_backoff_factor",
        "retry_backoff_max",
        "retry_mode",
        "retry_on_status_codes",
        *_always_reserved
    ],
    PadType.Model: [
        *_always_reserved
    ],
    PadType.Property: [
        "self",
        *_always_reserved
    ],
    PadType.Enum: [
        "mro",
        *_always_reserved
    ]
}
