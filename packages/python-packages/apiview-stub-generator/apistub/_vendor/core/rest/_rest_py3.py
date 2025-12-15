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
import abc
from typing import (
    Any,
    Iterable,
    Iterator,
    Optional,
    Union,
    MutableMapping,
)

ContentType = Union[str, bytes, Iterable[bytes]]

################################## CLASSES ######################################


class _HttpResponseBase(abc.ABC):
    """Base abstract base class for HttpResponses."""

    @property
    @abc.abstractmethod
    def request(self):
        """The request that resulted in this response.

        :rtype: ~azure.core.rest.HttpRequest
        :return: The request that resulted in this response.
        """

    @property
    @abc.abstractmethod
    def status_code(self) -> int:
        """The status code of this response.

        :rtype: int
        :return: The status code of this response.
        """

    @property
    @abc.abstractmethod
    def headers(self) -> MutableMapping[str, str]:
        """The response headers. Must be case-insensitive.

        :rtype: MutableMapping[str, str]
        :return: The response headers. Must be case-insensitive.
        """

    @property
    @abc.abstractmethod
    def reason(self) -> str:
        """The reason phrase for this response.

        :rtype: str
        :return: The reason phrase for this response.
        """

    @property
    @abc.abstractmethod
    def content_type(self) -> Optional[str]:
        """The content type of the response.

        :rtype: str
        :return: The content type of the response.
        """

    @property
    @abc.abstractmethod
    def is_closed(self) -> bool:
        """Whether the network connection has been closed yet.

        :rtype: bool
        :return: Whether the network connection has been closed yet.
        """

    @property
    @abc.abstractmethod
    def is_stream_consumed(self) -> bool:
        """Whether the stream has been consumed.

        :rtype: bool
        :return: Whether the stream has been consumed.
        """

    @property
    @abc.abstractmethod
    def encoding(self) -> Optional[str]:
        """Returns the response encoding.

        :return: The response encoding. We either return the encoding set by the user,
         or try extracting the encoding from the response's content type. If all fails,
         we return `None`.
        :rtype: optional[str]
        """

    @encoding.setter
    def encoding(self, value: Optional[str]) -> None:
        """Sets the response encoding.

        :param optional[str] value: The encoding to set
        """

    @property
    @abc.abstractmethod
    def url(self) -> str:
        """The URL that resulted in this response.

        :rtype: str
        :return: The URL that resulted in this response.
        """

    @property
    @abc.abstractmethod
    def content(self) -> bytes:
        """Return the response's content in bytes.

        :rtype: bytes
        :return: The response's content in bytes.
        """

    @abc.abstractmethod
    def text(self, encoding: Optional[str] = None) -> str:
        """Returns the response body as a string.

        :param optional[str] encoding: The encoding you want to decode the text with. Can
         also be set independently through our encoding property
        :return: The response's content decoded as a string.
        :rtype: str
        """

    @abc.abstractmethod
    def json(self) -> Any:
        """Returns the whole body as a json object.

        :return: The JSON deserialized response body
        :rtype: any
        :raises json.decoder.JSONDecodeError: if the body is not valid JSON.
        """

    @abc.abstractmethod
    def raise_for_status(self) -> None:
        """Raises an HttpResponseError if the response has an error status code.

        If response is good, does nothing.

        :raises ~azure.core.HttpResponseError: if the object has an error status code.
        """


class HttpResponse(_HttpResponseBase):
    """Abstract base class for HTTP responses.

    Use this abstract base class to create your own transport responses.

    Responses implementing this ABC are returned from your client's `send_request` method
    if you pass in an :class:`~azure.core.rest.HttpRequest`

    >>> from azure.core.rest import HttpRequest
    >>> request = HttpRequest('GET', 'http://www.example.com')
    <HttpRequest [GET], url: 'http://www.example.com'>
    >>> response = client.send_request(request)
    <HttpResponse: 200 OK>
    """

    @abc.abstractmethod
    def __enter__(self) -> "HttpResponse": ...

    @abc.abstractmethod
    def __exit__(self, *args: Any) -> None: ...

    @abc.abstractmethod
    def close(self) -> None: ...

    @abc.abstractmethod
    def read(self) -> bytes:
        """Read the response's bytes.

        :return: The read in bytes
        :rtype: bytes
        """

    @abc.abstractmethod
    def iter_raw(self, **kwargs: Any) -> Iterator[bytes]:
        """Iterates over the response's bytes. Will not decompress in the process.

        :return: An iterator of bytes from the response
        :rtype: Iterator[str]
        """

    @abc.abstractmethod
    def iter_bytes(self, **kwargs: Any) -> Iterator[bytes]:
        """Iterates over the response's bytes. Will decompress in the process.

        :return: An iterator of bytes from the response
        :rtype: Iterator[str]
        """

    def __repr__(self) -> str:
        content_type_str = ", Content-Type: {}".format(self.content_type) if self.content_type else ""
        return "<HttpResponse: {} {}{}>".format(self.status_code, self.reason, content_type_str)
