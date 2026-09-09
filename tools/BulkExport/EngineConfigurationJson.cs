using System.Globalization;
using System.Text;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// Deterministic engine-configuration description for the
    /// manifest (M10.0 §7 engine identity). Single line, fixed key
    /// order, invariant culture — the values are the composition
    /// facts of the export, matching PresetFactory exactly.
    /// </summary>
    public static class EngineConfigurationJson
    {
        public static string Render(ExportMode mode)
        {
            var sb = new StringBuilder(256);

            sb.Append("{\"reference_mode\":");

            if (mode == ExportMode.ATRSmooth2)
            {
                sb.Append("\"ATRSmooth2\"");
                sb.Append(",\"atr_period\":16");
                sb.Append(",\"atr_multiplier\":\"5.1\"");
                sb.Append(",\"smooth_length\":100");
            }
            else if (mode == ExportMode.DarvasBox)
            {
                sb.Append("\"DarvasBox\"");
                sb.Append(",\"darvas_length\":5");
            }
            else if (mode == ExportMode.Hma)
            {
                sb.Append("\"Hma\"");
                sb.Append(",\"hma_period\":16");
            }
            else
            {
                sb.Append("\"HmaAtrSmooth\"");
                sb.Append(",\"atr_period\":16");
                sb.Append(",\"atr_multiplier\":\"5.1\"");
                sb.Append(",\"smooth_length\":100");
                sb.Append(",\"hma_period\":16");
            }

            sb.Append(",\"scale_period\":14");
            sb.Append(",\"statistics_window_size\":252");
            sb.Append(",\"statistics_source\":\"Close\"");
            sb.Append(",\"reversal_mode\":\"TrailingStopPosition\"");
            sb.Append(",\"mean_darvas_window_size\":20");
            sb.Append(",\"mean_hma_atrsmooth_window_size\":20");
            sb.Append(",\"statistic_models\":[");
            sb.Append("\"Mean\",\"StandardDeviation\",\"Minimum\",\"Maximum\",");
            sb.Append("\"Median\",\"Variance\",\"MedianAbsoluteDeviation\",");
            sb.Append("\"Range\",\"Skewness\",\"Kurtosis\"");
            sb.Append("]}");

            return sb.ToString();
        }
    }
}
