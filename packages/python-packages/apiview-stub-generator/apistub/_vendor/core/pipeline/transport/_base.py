# --------------------------------------------------------------------------
#
# Copyright (c) Microsoft Corporation. All rights reserved.
#
# The MIT License (MIT)
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the ""Software""), to
# deal in the Software without restriction, including without limitation the
# rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
# sell copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
# FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
# IN THE SOFTWARE.
#
# --------------------------------------------------------------------------
from __future__ import annotations
import abc
import time

from typing import (
    Generic,
    TypeVar,
    Union,
    Any,
    Dict,
    ContextManager,
)

HTTPResponseType = TypeVar("HTTPResponseType")
HTTPRequestType = TypeVar("HTTPRequestType")
DataType = Union[bytes, str, Dict[str, Union[str, int]]]

binary_type = str

class HttpTransport(ContextManager["HttpTransport"], abc.ABC, Generic[HTTPRequestType, HTTPResponseType]):
    """An http sender ABC."""

    @abc.abstractmethod
    def send(self, request: HTTPRequestType, **kwargs: Any) -> HTTPResponseType:
        """Send the request using this HTTP sender.

        :param request: The pipeline request object
        :type request: ~azure.core.transport.HTTPRequest
        :return: The pipeline response object.
        :rtype: ~azure.core.pipeline.transport.HttpResponse
        """

    @abc.abstractmethod
    def open(self) -> None:
        """Assign new session if one does not already exist."""

    @abc.abstractmethod
    def close(self) -> None:
        """Close the session if it is not externally owned."""

    def sleep(self, duration: float) -> None:
        """Sleep for the specified duration.

        You should always ask the transport to sleep, and not call directly
        the stdlib. This is mostly important in async, as the transport
        may not use asyncio but other implementations like trio and they have their own
        way to sleep, but to keep design
        consistent, it's cleaner to always ask the transport to sleep and let the transport
        implementor decide how to do it.

        :param float duration: The number of seconds to sleep.
        """
        time.sleep(duration)
