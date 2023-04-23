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
            Release release = r.GenerateReleaseNote();
            release.Version = "1.2.3";
            release.ReleaseDate = "2030.3.3";

            string baseline =
@"## 1.2.3 (2030.3.3)

### Breaking Changes in API

Removed method 'String MethodToBeDeleted()' in type Azure.ResourceManager.AppService.TestMethod
Removed method 'String MethodToChangeReturnType()' in type Azure.ResourceManager.AppService.TestMethod
Removed method 'String MethodToChangeParameter()' in type Azure.ResourceManager.AppService.TestMethod
Removed property 'String PropertyToBeDeleted' in type Azure.ResourceManager.AppService.TestProperty
Removed property method 'Get' for 'String PropertyToChangeToSet' in type Azure.ResourceManager.AppService.TestProperty
Removed property method 'Set' for 'String PropertyToChangeToGet' in type Azure.ResourceManager.AppService.TestProperty
Removed type 'Azure.ResourceManager.AppService.TypeToBeDeleted'

### Other Changes in API

Added method 'String MethodAdded()' in type Azure.ResourceManager.AppService.TestMethod
Added method 'Int32 MethodToChangeReturnType()' in type Azure.ResourceManager.AppService.TestMethod
Added method 'String MethodToChangeParameter(Int32 someParam)' in type Azure.ResourceManager.AppService.TestMethod
Added method 'String MethodToChangeParameter<T>()' in type Azure.ResourceManager.AppService.TestMethod
Added method 'Response<Boolean> MethodToChangeParameter()' in type Azure.ResourceManager.AppService.TestMethod
Added property 'String PropertyAdded' in type Azure.ResourceManager.AppService.TestProperty
Added property method 'Set' for 'String PropertyToChangeToSet' in type Azure.ResourceManager.AppService.TestProperty
Added property method 'Get' for 'String PropertyToChangeToGet' in type Azure.ResourceManager.AppService.TestProperty
Added type 'Azure.ResourceManager.AppService.TypeAdded'
Added type 'Azure.ResourceManager.AppService.TypeAdded2<T>'
Obsoleted method 'String MethodToBeObsoleted()' in type Azure.ResourceManager.AppService.TestMethod
Obsoleted property 'String PropertyToBeObsoleted' in type Azure.ResourceManager.AppService.TestProperty
Obsoleted type 'Azure.ResourceManager.AppService.TypeToBeObsoleted'";
            string actual = release.ToString();
            Assert.AreEqual(baseline.Replace("\r\n", "\n"), actual.Replace("\r\n", "\n"));
        }
    }

}
