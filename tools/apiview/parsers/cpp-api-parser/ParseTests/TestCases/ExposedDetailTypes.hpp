// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

namespace Azure { namespace Storage {
  namespace _detail {
    struct Foo;

    enum class Mode
    {
      First,
      Second
    };

    struct Imported
    {
      int ImportedValue;
    };

    struct Hidden
    {
      int HiddenValue;
    };
    struct Unused
    {
      int UnusedValue;
    };
    using HiddenAlias = Hidden;

  } // namespace _detail

  using Foo = _detail::Foo;
  using AnotherFoo = _detail::Foo;
  using Mode = _detail::Mode;
  using _detail::Imported;

  namespace _detail {
    /** A publicly exposed detail type. */
    struct Foo
    {
      /** The exposed value. */
      int Value;
      void DoSomething();

    private:
      using PrivateAlias = Hidden;
      int PrivateValue;
    };
  } // namespace _detail

  namespace _internal {
    using InternalAlias = _detail::Foo;
  }
}} // namespace Azure::Storage
