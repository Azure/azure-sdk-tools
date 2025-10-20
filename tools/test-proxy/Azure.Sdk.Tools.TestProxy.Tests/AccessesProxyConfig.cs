using Xunit;

[CollectionDefinition("AccessesProxyConfig", DisableParallelization = true)]
public class AccessesProxyConfigCollection : ICollectionFixture<AccessesProxyConfigResetFixture> { }

public sealed class AccessesProxyConfigResetFixture { }
