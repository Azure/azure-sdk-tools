// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

// The same APIs in another file, with source lines moved.
namespace Azure { namespace AnonymousTest {
  struct Base {
    int Flags;
  };

  struct Options {

    /** Conditions for the operation. */
    struct : public Base {
      int Value;
    } Conditions;

    struct {
      int Other;
    } Extra;

    union {
      int Code;
      float Fraction;
    } Result;

    struct {

      struct {
        int Deep;
      } Inner;

    } Outer;
  };
}}
