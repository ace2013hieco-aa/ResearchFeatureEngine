using Xunit;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Normalization;
using ResearchFeatureEngine.Normalization.Models;

namespace ResearchFeatureEngine.Tests.Normalization
{
    public sealed class NormalizationEngineIntegrationTests
    {
        [Fact]
        public void Update_ComputesAndPublishes_NormalizedMeasurement()
        {
            // Arrange

            var values = new EngineValues();
            values.Distance.AbsoluteExtension = 12.0;
            values.Scale.Scale = 3.0;

            var context = new EngineContext(
                marketData: null!,
                values: values);

            var model = new ScaleNormalizationModel();

            var engine = new NormalizationEngine(
                context,
                model);

            // Act

            engine.Update();

            // Assert

            Assert.Equal(
                4.0,
                values.Normalization.NormalizedMeasurement,
                10);
        }
    }
}
