#!/usr/bin/env python
"""Multi-engine pipeline example for research-feature-engine.

Runs all four reference modes (ATRSmooth2, DarvasBox, Hma,
HmaAtrSmooth) over the same market data in a single script,
demonstrating how to chain engines and compare outputs.

Usage:
    python examples/multi_engine_pipeline.py
"""

import os
import sys

_REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, _REPO_ROOT)

from research_feature_engine import (
    ATRSmooth2,
    DarvasBox,
    Hma,
    HmaAtrSmooth,
    MarketData,
    EngineOptions,
    ReversalMode,
)


CSV_PATH = os.path.join(_REPO_ROOT, "Tests", "TestData", "EURUSD_M1_10000.csv")


def main() -> None:
    md = MarketData.from_csv(CSV_PATH)
    print(f"Loaded {md.count} bars from {CSV_PATH}")

    options = EngineOptions(
        statistics_window_size=20,
        reversal_mode=ReversalMode.TrailingStopPosition,
    )

    engines = [
        ("ATRSmooth2",   ATRSmooth2( md, atr_period=16,  atr_multiplier=5.1, smooth_length=100, options=options)),
        ("DarvasBox",    DarvasBox(  md, box_length=5,                                       options=options)),
        ("Hma",          Hma(        md, period=16,                                        options=options)),
        ("HmaAtrSmooth", HmaAtrSmooth(md, atr_period=14, atr_multiplier=2.0, smooth_length=100, hma_period=16, options=options)),
    ]

    summary = {}
    for name, engine in engines:
        df = engine.run()
        summary[name] = len(df)
        last = df.iloc[-1]
        print(f"\n{'='*60}")
        print(f"Engine: {name}")
        print(f"  Rows: {len(df)}")
        print(f"  Last reference_price: {last['reference_price']:.6f}")
        print(f"  Last directional_distance: {last['directional_distance']:.6f}")
        print(f"  Last scale: {last['scale']:.6f}")
        print(f"  Reversals detected: {int(last['is_reversal_bar'])}")

    print(f"\n{'='*60}")
    print("Summary:")
    for name, rows in summary.items():
        print(f"  {name:20s} → {rows} rows")


if __name__ == "__main__":
    main()