"""Market data loading helpers for research-feature-engine.

Provides a thin wrapper around the C# ``PythonMarketData`` adapter that
accepts pandas DataFrames or numpy arrays and feeds them to the engine.
"""

from __future__ import annotations

from typing import Any, Optional

import os
from pathlib import Path

try:
    import numpy as np
except ImportError:  # pragma: no cover
    np = None  # type: ignore

try:
    import pandas as pd
except ImportError:  # pragma: no cover
    pd = None  # type: ignore


def _to_float64_list(arr: Any) -> list[float]:
    """Convert a numpy array / pandas Series / list to a Python list of floats.

    pythonnet marshals a Python ``list[float]`` into a C# ``double[]``
    automatically, but numpy arrays need explicit conversion to avoid
    type-mismatch errors.
    """
    if np is not None and isinstance(arr, np.ndarray):
        arr = arr.astype(np.float64).flatten().tolist()
    elif isinstance(arr, list):
        arr = [float(x) for x in arr]
    else:
        arr = [float(x) for x in arr]
    return arr


def _to_datetime_strings(arr: Any) -> Optional[list[str]]:
    """Convert a datetime-like array to ISO-8601 strings for the .NET side."""
    if arr is None:
        return None
    if pd is not None and isinstance(arr, (pd.DatetimeIndex, pd.Series)):
        return [ts.isoformat() for ts in arr]
    if np is not None and isinstance(arr, np.ndarray):
        return [str(x) for x in arr]
    return [str(x) for x in arr]


def _resolve_column(df: "pd.DataFrame", candidates: list[str],
                    column_map: Optional[dict] = None) -> str:
    """Resolve a column from a DataFrame, trying case-insensitive matches."""
    columns_lower = {c.lower(): c for c in df.columns}
    for cand in candidates:
        if column_map and cand in column_map:
            actual = column_map[cand]
            if actual in df.columns:
                return actual
        lowered = cand.lower()
        if lowered in columns_lower:
            return columns_lower[lowered]
    raise KeyError(
        f"Could not find column matching {candidates} in DataFrame "
        f"with columns: {list(df.columns)}"
    )


class MarketData:
    """Python-side market data container backed by the .NET
    ``PythonMarketData`` adapter.

    Accepts a pandas ``DataFrame`` with columns ``open, high, low,
    close, volume`` and an optional datetime index, or numpy arrays
    passed positionally.

    Usage::

        md = MarketData.from_dataframe(df)
        engine.run(md)
    """

    def __init__(
        self,
        open: Any,
        high: Any,
        low: Any,
        close: Any,
        volume: Any,
        time: Any = None,
    ) -> None:
        from research_feature_engine import _load_dotnet, _get_type
        _load_dotnet()
        import System

        # Convert Python lists / numpy arrays to float lists
        open_arr = _to_float64_list(open)
        high_arr = _to_float64_list(high)
        low_arr = _to_float64_list(low)
        close_arr = _to_float64_list(close)
        volume_arr = _to_float64_list(volume)
        time_arr = _to_datetime_strings(time)

        # Instantiate the C# adapter
        md_type = _get_type(
            "ResearchFeatureEngine.Adapters.PythonMarketData",
            "ResearchFeatureEngine.Python",
        )
        instance = System.Activator.CreateInstance(md_type)
        instance.LoadFromArrays(
            open_arr, high_arr, low_arr, close_arr, volume_arr, time_arr
        )
        self._instance: Any = instance
        self._close: list[float] = close_arr
        self.count: int = len(close_arr)

    @property
    def instance(self) -> Any:
        """Returns the underlying .NET ``IMarketData`` instance.

        Only available after construction succeeds.
        """
        return self._instance

    @staticmethod
    def from_dataframe(
        df: "pd.DataFrame",
        column_map: Optional[dict] = None,
    ) -> "MarketData":
        """Create a ``MarketData`` from a pandas DataFrame.

        Looks for columns named (case-insensitive) ``open``,
        ``high``, ``low``, ``close``, ``volume``. Accepts common
        capitalizations like ``Open``, ``High``, etc.

        Parameters
        ----------
        df : pandas.DataFrame
            OHLCV DataFrame.
        column_map : dict, optional
            Override column names, e.g.
            ``{"datetime": "Timestamp"}``.
        """
        if pd is None:
            raise ImportError(
                "pandas is required. Install with: pip install pandas"
            )
        if df is None or len(df) == 0:
            raise ValueError("DataFrame is empty.")

        open_col = _resolve_column(df, ["open", "o"], column_map)
        high_col = _resolve_column(df, ["high", "h"], column_map)
        low_col = _resolve_column(df, ["low", "l"], column_map)
        close_col = _resolve_column(df, ["close", "c", "price"], column_map)
        volume_col = _resolve_column(df, ["volume", "vol", "v"], column_map)

        open_data = df[open_col].tolist()
        high_data = df[high_col].tolist()
        low_data = df[low_col].tolist()
        close_data = df[close_col].tolist()
        volume_data = df[volume_col].tolist()

        # Try to use a datetime column or the index for timestamps
        time_data: Any = None
        datetime_col = _resolve_column(
            df, ["datetime", "timestamp", "time", "date"], column_map
        ) if any(
            c.lower() in df.columns for c in ["datetime", "timestamp", "time", "date"]
        ) else None

        if datetime_col:
            time_data = df[datetime_col]
        elif isinstance(df.index, pd.DatetimeIndex):
            time_data = df.index

        return MarketData(
            open_data, high_data, low_data, close_data, volume_data, time_data
        )

    @staticmethod
    def from_csv(csv_path: str, delimiter: str = ",") -> "MarketData":
        """Load market data from a CSV file.

        Auto-detects the datetime column (case-insensitive match for
        ``datetime``, ``timestamp``, ``time``, or ``date``).
        """
        if pd is None:
            raise ImportError(
                "pandas is required. Install with: pip install pandas"
            )
        df = pd.read_csv(csv_path)
        # Find datetime column case-insensitively
        dt_col = None
        for c in df.columns:
            if c.lower() in ("datetime", "timestamp", "time", "date"):
                dt_col = c
                break
        if dt_col:
            df[dt_col] = pd.to_datetime(df[dt_col])
        return MarketData.from_dataframe(df)


# Convenience alias
load_market_data = MarketData.from_dataframe