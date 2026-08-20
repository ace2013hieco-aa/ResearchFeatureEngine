namespace ResearchFeatureEngine.Tests
{
    internal sealed class ExpectedResults
    {
        public ReferenceExpected Reference { get; } = new();
        public DistanceExpected Distance { get; } = new();
        public ScaleExpected Scale { get; } = new();
        public NormalizationExpected Normalization { get; } = new();
        public StatisticsExpected Statistics { get; } = new();
    }

    internal sealed class ReferenceExpected
    {
        public double Price { get; set; }
    }

    internal sealed class DistanceExpected
    {
        public double DirectionalExtension { get; set; }
        public double AbsoluteExtension { get; set; }
    }

    internal sealed class ScaleExpected
    {
        public double Scale { get; set; }
    }

    internal sealed class NormalizationExpected
    {
        public double NormalizedMeasurement { get; set; }
    }

    internal sealed class StatisticsExpected
    {
        public StatisticsLocationExpected Location { get; } = new();
        public StatisticsDispersionExpected Dispersion { get; } = new();
        public StatisticsRangeExpected Range { get; } = new();
    }

    internal sealed class StatisticsLocationExpected
    {
        public double Mean { get; set; }
        public double Median { get; set; }
    }

    internal sealed class StatisticsDispersionExpected
    {
        public double Variance { get; set; }
        public double StandardDeviation { get; set; }
        public double MedianAbsoluteDeviation { get; set; }
    }

    internal sealed class StatisticsRangeExpected
    {
        public double Minimum { get; set; }
        public double Maximum { get; set; }
    }
}
