// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Hubbup.MikLabelModel
{
    public interface IMikLabelerPathProvider
    {
        (string issuePath, string prPath) GetModelPath();
    }
}
