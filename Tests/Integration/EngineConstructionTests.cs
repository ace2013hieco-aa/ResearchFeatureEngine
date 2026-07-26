using ResearchFeatureEngine.Composition;
using Xunit;

namespace ResearchFeatureEngine.Tests.Integration;

public sealed class EngineConstructionTests
{
    [Fact]
    public void Build_WithValidConfiguration_ReturnsInitializedEngine()
    {
        // Arrange
        EngineConfiguration configuration =
            TestConfigurationFactory.Create();

        // Act
        ResearchFeatureEngine engine =
            new ResearchFeatureEngineBuilder(configuration)
                .Build();

        // Assert
        Assert.NotNull(engine);

        Assert.NotNull(engine.Context);

        Assert.NotNull(engine.Pipeline);

        Assert.NotNull(engine.Values);

        Assert.Same(engine.Context.Values, engine.Values);
    }
}
