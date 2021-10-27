// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import java.util.List;

/**
 * Result for a codesnippet operation.
 *
 * @param <T> Type of the result object.
 */
public final class SnippetOperationResult<T> {
    public T result;
    List<VerifyResult> errorList;

    public SnippetOperationResult(T resultObject, List<VerifyResult> errors) {
        this.result = resultObject;
        this.errorList = errors;
    }
}
