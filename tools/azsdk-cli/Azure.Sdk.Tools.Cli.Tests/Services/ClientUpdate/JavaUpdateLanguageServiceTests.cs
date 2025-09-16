using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services; // ILanguageSpecificCheckResolver, ILanguageSpecificChecks
using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Services.ClientUpdate;

[TestFixture]
public class JavaUpdateLanguageServiceTests
{
	private class DummyResolver : ILanguageSpecificCheckResolver
	{
		public Task<ILanguageSpecificChecks?> GetLanguageCheckAsync(string packagePath) => Task.FromResult<ILanguageSpecificChecks?>(null);
	}

	private class StubJavaUpdateLanguageService : JavaUpdateLanguageService
	{
		private readonly List<ApiChange> _return;
		public StubJavaUpdateLanguageService(List<ApiChange> returnChanges) : base(new DummyResolver(), NullLogger<JavaUpdateLanguageService>.Instance)
		{
			_return = returnChanges;
		}
		protected override Task<List<ApiChange>> RunExternalJavaApiDiffAsync(string oldPath, string newPath, CancellationToken ct) => Task.FromResult(_return);
	}

	[Test]
	public async Task ComputeApiChanges_UsesExternalToolResult()
	{
		var expected = new List<ApiChange>
		{
			new ApiChange { Kind = "MethodSignatureChanged", Symbol = "com.example.Client#beginOperation(String,OperationOptions)", Detail = "sig", Metadata = new() { {"oldParamNames","modelId,operationOptions"},{"newParamNames","modelId,operationRequest"},{"paramNameChange","true"} } },
			new ApiChange { Kind = "MethodAdded", Symbol = "com.example.Client#listABC()", Detail = "added" }
		};
		var svc = new StubJavaUpdateLanguageService(expected);
		var result = await svc.DiffAsync(oldGenerationPath: "old", newGenerationPath: "new");
		Assert.That(result, Has.Count.EqualTo(expected.Count));
		Assert.That(result[0].Kind, Is.EqualTo("MethodSignatureChanged"));
		Assert.That(result[0].Metadata["paramNameChange"], Is.EqualTo("true"));
		Assert.That(result[1].Kind, Is.EqualTo("MethodAdded"));
	}

	[Test]
	public void ComputeApiChanges_ThrowsIfPathsMissing()
	{
		var svc = new StubJavaUpdateLanguageService(new List<ApiChange>());
		Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.DiffAsync(string.Empty, "new"));
		Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.DiffAsync("old", string.Empty));
	}
}
