// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include <chrono>
#include <map>
#include <string>
#include <vector>
#include <memory>

namespace Outer1 {
class OuterClass1 final {
private:
  void PFunction1(std::chrono::system_clock::time_point const& time1) {}
  int PFunction2(std::chrono::system_clock::time_point const& time1) { return 0; }

public:
};

namespace Cryptography {
  class Hash {
  protected:
    explicit Hash() = default;

  public:
    void Append(const uint8_t* data, size_t length);
    std::vector<uint8_t> Final(const uint8_t* data, size_t length);
    std::vector<uint8_t> Final();
    virtual ~Hash() = default;
  };
  class Md5Hash : public Cryptography::Hash {
  public:
    Md5Hash();
    virtual ~Md5Hash();
    Md5Hash(const Md5Hash&) = default;
    Cryptography::Md5Hash& operator=(const Cryptography::Md5Hash&);
  };
} // namespace Cryptography

int Function2(std::chrono::system_clock::time_point const& time1) { return 0; }

namespace _internal {
  class OperationStatus {
  public:
    OperationStatus(const std::string& value);
    OperationStatus(std::string&& value);
    OperationStatus(const char* value);
    bool operator==(const OperationStatus& other);
    bool operator!=(const OperationStatus& other);
    const std::string& Get();
    static const OperationStatus NotStarted;
    static const OperationStatus Running;
    static const OperationStatus Succeeded;
    static const OperationStatus Cancelled;
    static const OperationStatus Failed;
    OperationStatus(const OperationStatus&) = default;
    OperationStatus(OperationStatus&&) = default;
    OperationStatus& operator=(const OperationStatus&);
    OperationStatus& operator=(OperationStatus&&);
    ~OperationStatus() = default;
  };
} // namespace _internal

enum class HttpStatusCode : int
{
  None = 0,
  Continue = 100,
  SwitchingProtocols = 101,
  Processing = 102,
  EarlyHints = 103,
  Ok = 200,
  Created = 201,
  Accepted = 202,
  NonAuthoritativeInformation = 203,
  NoContent = 204,
  ResetContent = 205,
  PartialContent = 206,
  MultiStatus = 207,
  AlreadyReported = 208,
  IMUsed = 226,
  MultipleChoices = 300,
  MovedPermanently = 301,
  Found = 302,
  SeeOther = 303,
  NotModified = 304,
  UseProxy = 305,
  TemporaryRedirect = 307,
  PermanentRedirect = 308,
  BadRequest = 400,
  Unauthorized = 401,
  PaymentRequired = 402,
  Forbidden = 403,
  NotFound = 404,
  MethodNotAllowed = 405,
  NotAcceptable = 406,
  ProxyAuthenticationRequired = 407,
  RequestTimeout = 408,
  Conflict = 409,
  Gone = 410,
  LengthRequired = 411,
  PreconditionFailed = 412,
  PayloadTooLarge = 413,
  UriTooLong = 414,
  UnsupportedMediaType = 415,
  RangeNotSatisfiable = 416,
  ExpectationFailed = 417,
  MisdirectedRequest = 421,
  UnprocessableEntity = 422,
  Locked = 423,
  FailedDependency = 424,
  TooEarly = 425,
  UpgradeRequired = 426,
  PreconditionRequired = 428,
  TooManyRequests = 429,
  RequestHeaderFieldsTooLarge = 431,
  UnavailableForLegalReasons = 451,
  InternalServerError = 500,
  NotImplemented = 501,
  BadGateway = 502,
  ServiceUnavailable = 503,
  GatewayTimeout = 504,
  HttpVersionNotSupported = 505,
  VariantAlsoNegotiates = 506,
  InsufficientStorage = 507,
  LoopDetected = 508,
  NotExtended = 510,
  NetworkAuthenticationRequired = 511,
};
class RawResponse {
public:
  RawResponse(
      int32_t majorVersion,
      int32_t minorVersion,
      HttpStatusCode statusCode,
      const std::string& reasonPhrase);
  RawResponse(const RawResponse& response);
  RawResponse(RawResponse&& response) = default;
  RawResponse& operator=(const RawResponse&);
  RawResponse& operator=(RawResponse&&);
  ~RawResponse() = default;
  void SetHeader(const std::string& name, const std::string& value);
  void SetBody(std::vector<uint8_t> body);
  HttpStatusCode GetStatusCode();
  const std::string& GetReasonPhrase();
  std::vector<uint8_t>& GetBody();
};
class HttpPolicy {
public:
  virtual std::unique_ptr<RawResponse> Send() = 0;
  virtual ~HttpPolicy();
  virtual std::unique_ptr<HttpPolicy> Clone() = 0;

protected:
  explicit HttpPolicy() = default;
  explicit HttpPolicy(const HttpPolicy& other) = default;
  HttpPolicy& operator=(const HttpPolicy& other);
  explicit HttpPolicy(HttpPolicy&& other) = default;
};
namespace _internal {
  class TransportPolicy : public HttpPolicy {
  public:
    TransportPolicy();
    virtual std::unique_ptr<HttpPolicy> Clone();
    virtual std::unique_ptr<RawResponse> Send();
    TransportPolicy& operator=(const TransportPolicy&);
    TransportPolicy& operator=(TransportPolicy&&);
    virtual ~TransportPolicy() = default;
  };
  class RetryPolicy : public HttpPolicy, RawResponse {
  public:
    RetryPolicy();
    virtual std::unique_ptr<HttpPolicy> Clone();
    virtual std::unique_ptr<RawResponse> Send();
    int32_t GetRetryCount();

  protected:
    virtual bool ShouldRetryOnTransportFailure(
        int32_t attempt,
        std::chrono::milliseconds& retryAfter,
        double jitterFactor);
    virtual bool ShouldRetryOnResponse(
        int32_t attempt,
        std::chrono::milliseconds& retryAfter,
        double jitterFactor);
    RetryPolicy(const RetryPolicy&) = default;
    RetryPolicy(RetryPolicy&&) = default;
    RetryPolicy& operator=(const RetryPolicy&);
    RetryPolicy& operator=(RetryPolicy&&);
    virtual ~RetryPolicy() = default;
  };
  class RequestIdPolicy : public HttpPolicy, private RawResponse {
  public:
    RequestIdPolicy();
    virtual std::unique_ptr<HttpPolicy> Clone();
    virtual std::unique_ptr<RawResponse> Send();
    explicit RequestIdPolicy(const RequestIdPolicy&) = default;
    explicit RequestIdPolicy(RequestIdPolicy&&) = default;
    RequestIdPolicy& operator=(const RequestIdPolicy&);
    RequestIdPolicy& operator=(RequestIdPolicy&&);
    virtual ~RequestIdPolicy() = default;
  };
  class RequestActivityPolicy : public HttpPolicy {
  public:
    RequestActivityPolicy();
    virtual std::unique_ptr<HttpPolicy> Clone();
    virtual std::unique_ptr<RawResponse> Send();
    RequestActivityPolicy(const RequestActivityPolicy&) = default;
    RequestActivityPolicy(RequestActivityPolicy&&) = default;
    RequestActivityPolicy& operator=(const RequestActivityPolicy&);
    RequestActivityPolicy& operator=(RequestActivityPolicy&&);
    virtual ~RequestActivityPolicy() = default;
  };
  class TelemetryPolicy : public HttpPolicy {
  public:
    TelemetryPolicy(const std::string& componentName, const std::string& componentVersion);
    explicit TelemetryPolicy(std::string cn);
    explicit TelemetryPolicy(int val = 35);
    explicit TelemetryPolicy() = delete;
    virtual std::unique_ptr<HttpPolicy> Clone();
    virtual std::unique_ptr<RawResponse> Send();
    TelemetryPolicy(const TelemetryPolicy&) = default;
    TelemetryPolicy(TelemetryPolicy&&) = default;
    TelemetryPolicy& operator=(const TelemetryPolicy&);
    TelemetryPolicy& operator=(TelemetryPolicy&&);
    virtual ~TelemetryPolicy() = default;
  };

  class MultipleFields {
  public:
    static bool StaticField;
    const bool ConstField;
    static const bool StaticConstField;
    mutable bool MutableField;
    bool Field;
  };
  struct MultipleFieldsStruct {
    static bool StaticField;
    const bool ConstField;
    static const bool StaticConstField;
    bool Field;
  };
} // namespace _internal
} // namespace Outer1
