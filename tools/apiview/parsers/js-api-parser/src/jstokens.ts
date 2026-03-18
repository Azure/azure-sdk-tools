// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { type ReviewToken } from "./models";

/**
 * Returns a {@link ReviewToken} with HasSuffixSpace of false by default
 * @param options
 * @returns
 */
export function buildToken(options: ReviewToken): ReviewToken {
  return {
    ...options,
    HasSuffixSpace: options.HasSuffixSpace ?? false,
  };
}
