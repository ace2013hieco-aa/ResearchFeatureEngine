#!/usr/bin/env python
"""Basic indicator example for research-feature-engine.

Demonstrates loading market data from CSV and running a single
engine (ATRSmooth2) to obtain reference prices, regime signal,
distance, scale, normalization, and statistics per bar.

Usage:
    python examples/basic_indicator.py
"""

import os
import sys

# Ensure the repo root is on the path when running from the examples/
# directory directly.
_REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, _REPO_ROOT)

from research_feature_engine import ATRSmooth2, MarketData, EngineOptions, ReversalMode


CSV_PATH = os.path.join(_REPO_ROOT, "Tests", "TestData", "EURUSD_M1_10000.csv")


def main() -> None:
    # ── Load market data ──────────────────────────────────────────────────
    md = MarketData.from_csv(CSV_PATH)
    print(f"Loaded {md.count} bars from {CSV_PATH}")

    # ── Build the engine ──────────────────────────────────────────────────
    options = EngineOptions(
        statistics_window_size=20,
        reversal_mode=ReversalMode.TrailingStopPosition,
    )
    engine = ATRSmooth2(
        md,
        atr_period=16,
        atr_multiplier=5.1,
        smooth_length=100,
        options=options,
    )
    print("Engine built: ATRSmooth2 (VWMA + ATR trailing-stop)")

    # ── Run the pipeline ──────────────────────────────────────────────────
    df = engine.run()
    print(f"Results: {len(df)} rows × {len(df.columns)} columns")

    print(df[["index", "reference_price", "reference_regime",
               "directional_distance", "scale"]].head(10).to_string(index=False))


if __name__ == "__main__":
    main()