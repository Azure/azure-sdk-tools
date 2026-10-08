// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

namespace Azure { namespace TreeTest {
  /** A sample class with documentation. */
  class Widget {
  public:
    /** Returns a value. <a href="https://example.test/docs">Read more</a>. */
    int GetValue(int input, int other) const;
    struct Nested
    {
      int Field;
    };
    enum class Mode
    {
      First = 1,
      Second = 2
    };
    template <class T> T Transform(T value) const;
  };
  class Forward;
  class Defined;
  class Defined {};
  enum class ForwardEnum;
  class Final final {
  protected:
    bool ShouldRetry(int index) const;
  };
  void Overloaded(int value);
  void Overloaded(double value);
  template <class T> T Identity(T value);
}} // namespace Azure::TreeTest
