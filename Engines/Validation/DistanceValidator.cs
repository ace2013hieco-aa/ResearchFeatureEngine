using System;

namespace ResearchFeatureEngine.Engines.Validation
{
    public static class DistanceValidator
    {
        public static void Validate(double directionalExtension, double absoluteExtension)
        {
            if (double.IsNaN(directionalExtension))
                throw new InvalidOperationException(
                    "Directional extension cannot be NaN.");

            if (double.IsInfinity(directionalExtension))
                throw new InvalidOperationException(
                    "Directional extension cannot be infinite.");

            if (double.IsNaN(absoluteExtension))
                throw new InvalidOperationException(
                    "Absolute extension cannot be NaN.");

            if (double.IsInfinity(absoluteExtension))
                throw new InvalidOperationException(
                    "Absolute extension cannot be infinite.");

            if (absoluteExtension < 0.0)
                throw new InvalidOperationException(
                    $"Absolute extension is negative ({absoluteExtension}).");
        }
    }
}
