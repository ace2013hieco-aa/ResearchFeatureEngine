using System;

namespace ResearchFeatureEngine.Engines.Validation
{
    public static class ReferenceValidator
    {
        public static void Validate(double referencePrice)
        {
            if (double.IsNaN(referencePrice))
                throw new InvalidOperationException(
                    "Reference price cannot be NaN.");

            if (double.IsInfinity(referencePrice))
                throw new InvalidOperationException(
                    "Reference price cannot be infinite.");
        }
    }
}
