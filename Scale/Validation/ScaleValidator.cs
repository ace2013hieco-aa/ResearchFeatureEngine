using System;

namespace ResearchFeatureEngine.Scale.Validation
{
    public static class ScaleValidator
    {
        public static void Validate(double scale)
        {
            if (double.IsNaN(scale))
                throw new InvalidOperationException("Scale cannot be NaN.");

            if (double.IsInfinity(scale))
                throw new InvalidOperationException("Scale cannot be infinite.");

            if (scale <= 0.0)
                throw new InvalidOperationException("Scale must be greater than zero.");
        }
    }
}

