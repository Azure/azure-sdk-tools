// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.SDK.ChangelogGen.Compare;
using Azure.SDK.ChangelogGen.Report;

namespace Azure.SDK.ChangelogGen.Tests
{
    [TestClass]
    public class TestApiComparer
    {
        [TestMethod]
        public void TestCompareApiFile()
        {
            string content1 = File.ReadAllText("apiFile1.cs.txt");
            string content2 = File.ReadAllText("apiFile2.cs.txt");
            ChangeLogResult r = new ChangeLogResult();
            r.ApiChange = Program.CompareApi(content2, content1);
            Release release = r.GenerateReleaseNote("1.2.3", "2030.3.3", filter: new List<ChangeCatogory>() { ChangeCatogory.Obsoleted });

            // we dont expect any breaking change in our release
            // But in case any breaking changes detected, we will list them anyway so that people are able to notice these unexpected breaking changes when reviewing the changelog and fix them
            string baseline =
@"## 1.2.3 (2030.3.3)

### Breaking Changes

- Removed method 'String MethodToBeDeleted()' in type Azure.ResourceManager.AppService.TestMethod
- Removed method 'String MethodChangeDefaultValue(Int32 param = 0)' in type Azure.ResourceManager.AppService.TestMethod
- Removed method 'String MethodToChangeReturnType()' in type Azure.ResourceManager.AppService.TestMethod
- Removed method 'String MethodToChangeParameter()' in type Azure.ResourceManager.AppService.TestMethod
- Removed property 'String PropertyToBeDeleted' in type Azure.ResourceManager.AppService.TestProperty
- Removed property method 'Get' for 'String PropertyToChangeToSet' in type Azure.ResourceManager.AppService.TestProperty
- Removed property method 'Set' for 'String PropertyToChangeToGet' in type Azure.ResourceManager.AppService.TestProperty
- Removed type 'Azure.ResourceManager.AppService.TypeToBeDeleted'

### Other Changes

- Obsoleted method 'Void StaticMethodToBeObsoleted()' in type Azure.ResourceManager.AppService.StaticTypeToBeObsoleted
- Obsoleted method 'String MethodToBeObsoleted(String name, Int32 count, Boolean isEnabled, CancellationToken cancellationToken)' in type Azure.ResourceManager.AppService.TestMethod
- Obsoleted property 'String StaticPropertyToBeObsoleted' in type Azure.ResourceManager.AppService.StaticTypeToBeObsoleted
- Obsoleted property 'String PropertyToBeObsoleted' in type Azure.ResourceManager.AppService.TestProperty
- Obsoleted type 'Azure.ResourceManager.AppService.TypeToBeObsoleted'
- Obsoleted type 'Azure.ResourceManager.AppService.StaticTypeToBeObsoleted'";
            string actual = release.ToString();
            Assert.AreEqual(baseline.Replace("\r\n", "\n"), actual.Replace("\r\n", "\n"));
        }
    }

}
