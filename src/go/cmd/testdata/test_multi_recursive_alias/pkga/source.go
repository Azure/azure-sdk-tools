package pkga

import "github.com/Azure/azure-sdk-for-go/sdk/test_multi_recursive_alias/pkgb"

type Type1 = pkgb.Type1

type Type2 = pkgb.Type2

type Type3 = pkgb.Type3
