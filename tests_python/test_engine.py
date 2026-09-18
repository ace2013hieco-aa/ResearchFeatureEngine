"""Tests for the research_feature_engine Python package.

These tests run against the 10,000-row EURUSD M1 test fixture shipped
in ``Tests/TestData/``. They verify:

1. MarketData loads correctly (row count, column resolution).
2. Each of the four engines constructs and runs without error.
3. Engine output DataFrames have the expected columns and row count.
4. Numerical sanity checks (reference prices are non-null, distances
   are finite).
"""

import os
from pathlib import Path

import pytest

from research_feature_engine import (
    ATRSmooth2,
    DarvasBox,
    Hma,
    HmaAtrSmooth,
    EngineOptions,
    MarketData,
    ReferenceType,
    ReversalMode,
    StatisticsSource,
)

REPO_ROOT = Path(__file__).resolve().parent.parent
CSV_PATH = REPO_ROOT / "Tests" / "TestData" / "EURUSD_M1_10000.csv"

EXPECTED_ROWS = 10000


# ── Fixtures ──────────────────────────────────────────────────────────────

@pytest.fixture(scope="module")
def market_data() -> MarketData:
    """Load the EURUSD fixture once for all tests in this module."""
    md = MarketData.from_csv(str(CSV_PATH))
    assert md.count == EXPECTED_ROWS
    return md


@pytest.fixture
def engine_options() -> EngineOptions:
    return EngineOptions(
        statistics_window_size=20,
        reversal_mode=ReversalMode.TrailingStopPosition,
        statistics_source=StatisticsSource.Close,
    )


# ── MarketData tests ──────────────────────────────────────────────────────

class TestMarketData:
    def test_from_csv_row_count(self, market_data: MarketData):
        assert market_data.count == EXPECTED_ROWS

    def test_from_csv_columns_resolved(self, market_data: MarketData):
        # The engine should be able to run with this data
        engine = HmaAtrSmooth(market_data)
        df = engine.run()
        assert len(df) == EXPECTED_ROWS

    def test_from_dataframe(self):
        import pandas as pd
        df = pd.read_csv(str(CSV_PATH))
        md = MarketData.from_dataframe(df)
        assert md.count == EXPECTED_ROWS

    def test_empty_dataframe_raises(self):
        import pandas as pd
        df = pd.DataFrame()
        with pytest.raises(ValueError, match="empty"):
            MarketData.from_dataframe(df)

    def test_missing_columns_raises(self):
        import pandas as pd
        df = pd.DataFrame({"foo": [1, 2, 3]})
        with pytest.raises((KeyError, ValueError)):
            MarketData.from_dataframe(df)


# ── Engine construction tests ───────────────────────────────────────────────

class TestEngineConstruction:
    def test_atrsmooth2(self, market_data, engine_options):
        engine = ATRSmooth2(market_data, options=engine_options)
        assert engine.engine is not None

    def test_darvasbox(self, market_data, engine_options):
        engine = DarvasBox(market_data, options=engine_options)
        assert engine.engine is not None

    def test_hma(self, market_data, engine_options):
        engine = Hma(market_data, options=engine_options)
        assert engine.engine is not None

    def test_hma_atr_smooth(self, market_data, engine_options):
        engine = HmaAtrSmooth(market_data, options=engine_options)
        assert engine.engine is not None


# ── Engine run tests ───────────────────────────────────────────────────────

class TestEngineRun:
    ALL_ENGINES = [
        (ATRSmooth2, {"atr_period": 16, "atr_multiplier": 5.1, "smooth_length": 100}),
        (DarvasBox, {"box_length": 5}),
        (Hma, {"period": 21}),
        (HmaAtrSmooth, {"atr_period": 14, "atr_multiplier": 2.0,
                        "smooth_length": 100, "hma_period": 21}),
    ]

    @pytest.mark.parametrize("engine_cls,kwargs", ALL_ENGINES)
    def test_run_returns_dataframe(self, market_data, engine_options, engine_cls, kwargs):
        engine = engine_cls(market_data, options=engine_options, **kwargs)
        df = engine.run()
        assert len(df) == EXPECTED_ROWS
        assert "reference_price" in df.columns
        assert "directional_distance" in df.columns
        assert "scale" in df.columns
        assert "normalized_measurement" in df.columns

    @pytest.mark.parametrize("engine_cls,kwargs", ALL_ENGINES)
    def test_run_values_are_finite(self, market_data, engine_options, engine_cls, kwargs):
        """After warmup, reference prices and distances should be finite."""
        engine = engine_cls(market_data, options=engine_options, **kwargs)
        df = engine.run()
        # Check last 100 rows are finite (warmup period may have NaNs)
        tail = df.tail(100)
        assert tail["reference_price"].notna().all()
        # Distance should be finite (not inf)
        import numpy as np
        assert np.isfinite(tail["directional_distance"]).all()
        assert np.isfinite(tail["scale"]).all()

    @pytest.mark.parametrize("engine_cls,kwargs", ALL_ENGINES)
    def test_run_as_dict_list(self, market_data, engine_options, engine_cls, kwargs):
        engine = engine_cls(market_data, options=engine_options, **kwargs)
        results = engine.run(as_dataframe=False, as_dict_list=True)
        assert len(results) == EXPECTED_ROWS
        assert isinstance(results[0], dict)
        assert "reference_price" in results[0]


# ── Engine parameter tests ──────────────────────────────────────────────────

class TestEngineParameters:
    def test_atrsmooth2_custom_params(self, market_data, engine_options):
        engine = ATRSmooth2(market_data, atr_period=21, atr_multiplier=3.0,
                           smooth_length=50, options=engine_options)
        df = engine.run()
        assert len(df) == EXPECTED_ROWS

    def test_hma_custom_period(self, market_data, engine_options):
        engine = Hma(market_data, period=55, options=engine_options)
        df = engine.run()
        assert len(df) == EXPECTED_ROWS

    def test_hma_atr_smooth_custom_params(self, market_data, engine_options):
        engine = HmaAtrSmooth(market_data, atr_period=21, atr_multiplier=3.0,
                             smooth_length=50, hma_period=55,
                             options=engine_options)
        df = engine.run()
        assert len(df) == EXPECTED_ROWS


# ── Output content tests ────────────────────────────────────────────────────

class TestOutputContent:
    def test_hma_atr_smooth_reversal_bars(self, market_data, engine_options):
        """At least some reversal bars should be detected on real data."""
        engine = HmaAtrSmooth(market_data, options=engine_options)
        df = engine.run()
        reversal_count = int(df["is_reversal_bar"].sum())
        # EURUSD M1 10k should have several reversals
        assert reversal_count > 0

    def test_reference_regime_values(self, market_data, engine_options):
        """Regime should be in {-1, 0, +1}."""
        engine = ATRSmooth2(market_data, options=engine_options)
        df = engine.run()
        regimes = df["reference_regime"].dropna().unique()
        for r in regimes:
            assert r in (-1.0, 0.0, 1.0)
