#!/usr/bin/env python
"""Pandas integration example for research-feature-engine.

Shows how to construct a MarketData object from a pandas DataFrame,
run the HmaAtrSmooth engine, and join the engine's published values
back onto the original DataFrame for further analysis.

Usage:
    python examples/pandas_integration.py
"""

import os
import sys

_REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, _REPO_ROOT)

import pandas as pd

from research_feature_engine import MarketData, HmaAtrSmooth, EngineOptions


CSV_PATH = os.path.join(_REPO_ROOT, "Tests", "TestData", "EURUSD_M1_10000.csv")


def main() -> None:
    # ── Load into a pandas DataFrame ─────────────────────────────────────
    df = pd.read_csv(CSV_PATH)
    print(f"Loaded {len(df)} rows into pandas DataFrame")
    print(f"Columns: {list(df.columns)}")

    # ── Build MarketData from the DataFrame ──────────────────────────────
    md = MarketData.from_dataframe(df)
    print(f"\nMarketData instance: {md.count} bars")

    # ── Run the HmaAtrSmooth engine ──────────────────────────────────────
    engine = HmaAtrSmooth(md, atr_period=14, atr_multiplier=2.0,
                         smooth_length=100, hma_period=16)
    results = engine.run(as_dataframe=True)
    print(f"\nEngine results: {len(results)} rows")

    # ── Merge engine output back onto the original DataFrame ─────────────
    merged = df.join(results, how="left")
    print(f"\nMerged DataFrame: {merged.shape[0]} rows × {merged.shape[1]} columns")

    # Display a few interesting columns
    cols_of_interest = ["Close", "reference_price", "reference_regime",
                        "directional_distance", "scale", "normalized_measurement"]
    print("\nFirst 5 rows:")
    print(merged[cols_of_interest].head().to_string(index=False))

    print("\nLast 5 rows:")
    print(merged[cols_of_interest].tail().to_string(index=False))

    # ── Compute a custom signal from the engine output ───────────────────
    merged["signal"] = merged["reference_regime"]
    reversals = merged[merged["is_reversal_bar"]]
    print(f"\nTotal reversal bars: {len(reversals)}")

    # Save a subset to CSV for further analysis
    out_path = os.path.join(_REPO_ROOT, "examples", "output_pandas_integration.csv")
    merged[cols_of_interest + ["signal"]].to_csv(out_path, index=False)
    print(f"\nSaved {len(merged)} rows to {out_path}")


if __name__ == "__main__":
    main()