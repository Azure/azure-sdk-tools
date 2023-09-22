// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include <map>
#include <string>

struct HelperClass
{
  static const std::string HelperFunction() { return "Helper"; }
  static const char* ReturnsConstString() { return "Const string"; }
  static int Helper2(int one, float two, std::string three, std::map<int, float> four);
};

class TestClass {
public:
  std::string PublicField1;
  [[deprecated]]
  std::string PublicField2{"Test Constructor"};
  std::string PublicField3 = "Test Copy Initializer ";
  bool BoolField = true;
  bool BoolField2{false};
  [[deprecated("Use field BoolField2 instead.")]]
  bool BoolField3{};
  int IntField = 20;
  int IntField2{};
  float FloatField = static_cast<float>(3.7);
  char* ConstInt = const_cast<char*>(HelperClass::ReturnsConstString());
  float FloatField2 = 3.7f;
  double DoubleField{3.1415};
  /**
   * @brief The maximum number of bytes in a single request.
   */
  int64_t ChunkSize = 4 * 1024 * 1024;

  std::string HelperValue = HelperClass::HelperFunction();
  int Helper2 = HelperClass::Helper2(3, 4.5, "aab", {{1, 3.5f}, {2, 4.9f}});
};

/**
 * @brief Optional parameters for #Azure::Storage::Blobs::BlobClient::DownloadTo.
 */
struct DownloadBlobToOptions final
{
  /**
   * @brief Downloads only the bytes of the blob in the specified range.
   */
  double DoubleField{};

  /**
   * @brief Options for parallel transfer.
   */
  struct
  {
    /**
     * @brief The size of the first range request in bytes. Blobs smaller than this limit will be
     * downloaded in a single request. Blobs larger than this limit will continue being downloaded
     * in chunks of size ChunkSize.
     */
    int64_t InitialChunkSize = 256 * 1024 * 1024;

    /**
     * @brief The maximum number of bytes in a single request.
     */
    int64_t ChunkSize = 4 * 1024 * 1024;

    /**
     * @brief The maximum number of threads that may be used in a parallel transfer.
     */
    int32_t Concurrency = 5;
  } TransferOptions;
};

struct AnonymousInnerField
{
  int Field1;
  struct
  {
    bool InnerField1;
  };
};
struct AnonymousInnerField2
{
  int Field1;
  struct
  {
    bool InnerField1;
  } InnerStruct;
  struct
  {
    bool InnerField1;
  };
  struct
  {
    bool InnerField2;
  };
};

/**
 * @brief An Azure Storage blob.
 */
struct BlobItem final
{
  /**
   * Blob name.
   */
  std::string Name;
  /**
   * Indicates whether this blob was deleted.
   */
  bool IsDeleted = bool();
  bool IsDeleted2 = bool(true);
  bool IsDeleted3 = bool(false);
  /**
   * A string value that uniquely identifies a blob snapshot.
   */
  std::string Snapshot;
  /**
   * Properties of a blob.
   */
  DownloadBlobToOptions Details;
  /**
   * Size in bytes.
   */
  int64_t BlobSize = int64_t();

  float MyMethod1(
      int param1 = 378,
      std::string param2 = "foo",
      bool param3 = true,
      int param4 = -17,
      float param5 = 3.1415f)
  {
    return param1 + param3 + param4 + param5;
  }
};
namespace Meow {

enum Enumeration1
{
  Enumerator1,
  Enumerator2,
  Enumerator3,
};

enum class Enumeration2
{
  Enumerator1,
  Enumerator2,
  Enumerator3,
};

enum struct Enumeration3 : uint16_t
{
  Enumerator1,
  Enumerator2,
  Enumerator3,
};
} // namespace Meow
