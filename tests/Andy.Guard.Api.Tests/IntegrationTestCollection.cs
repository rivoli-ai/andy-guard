using Andy.Guard.Tests;

namespace Andy.Guard.Api.Tests;

public static class TestCollections
{
    public const string Integration = "Andy.Guard.IntegrationTests";
}

[CollectionDefinition(TestCollections.Integration, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<InferenceServiceFixture>
{
}
