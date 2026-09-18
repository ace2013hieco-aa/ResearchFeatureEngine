"""Core engine wrappers for research-feature-engine.

Provides Pythonic classes for the four reference modes:
``ATRSmooth2``, ``DarvasBox``, ``Hma``, and ``HmaAtrSmooth``.

Each wrapper constructs the corresponding .NET reference source,
builds the full engine pipeline (Reference -> Distance -> Scale ->
Normalization -> Statistics -> Reversal), drives it bar-by-bar, and
returns the published ``EngineValues`` as a pandas DataFrame.
"""

from __future__ import annotations

from typing import Any, Optional
from pathlib import Path


class ReferenceType:
    """Mirror of ``ResearchFeatureEngine.Core.ReferenceType``.

    Attributes
    ----------
    ATRSmooth2 : int
        Equilibrium-level reference (VWMA + ATR trailing stop).
    DarvasBox : int
        Box-midpoint reference.
    Hma : int
        Hull Moving Average reference.
    HmaAtrSmooth : int
        Composite dual-reference (HMA + ATRSmooth).
    """
    ATRSmooth2 = 0
    DarvasBox = 1
    Hma = 2
    HmaAtrSmooth = 3

    _NAMES = {
        ATRSmooth2: "ATRSmooth2",
        DarvasBox: "DarvasBox",
        Hma: "Hma",
        HmaAtrSmooth: "HmaAtrSmooth",
    }

    @classmethod
    def from_string(cls, name: str) -> int:
        name = name.strip().lower()
        mapping = {
            "atrsmooth2": cls.ATRSmooth2,
            "darvasbox": cls.DarvasBox,
            "hma": cls.Hma,
            "hmaatrsmooth": cls.HmaAtrSmooth,
        }
        if name not in mapping:
            raise ValueError(
                f"Unknown reference type: {name}. "
                f"Valid: ATRSmooth2, DarvasBox, Hma, HmaAtrSmooth"
            )
        return mapping[name]


class StatisticsSource:
    """Mirror of ``ResearchFeatureEngine.Core.StatisticsSource``."""
    Close = 0
    SimpleReturn = 1
    LogReturn = 2


class ReversalMode:
    """Mirror of ``ResearchFeatureEngine.Core.ReversalMode``."""
    CloseToReference = 0
    TrailingStopPosition = 1


class EngineOptions:
    """Python-side wrapper for engine configuration options.

    Parameters
    ----------
    statistics_window_size : int
        Rolling window size for statistics (default 20).
    mean_darvas_window_size : int
        Rolling window for mean Darvas closing distance (default 20).
    mean_hma_atr_smooth_window_size : int
        Rolling window for mean HMA-ATRSmooth distance (default 20).
    statistics_source : int
        Which series to compute statistics on. See ``StatisticsSource``.
    reversal_mode : int
        Reversal detection mode. See ``ReversalMode``.
    """

    def __init__(
        self,
        statistics_window_size: int = 20,
        mean_darvas_window_size: int = 20,
        mean_hma_atr_smooth_window_size: int = 20,
        statistics_source: int = StatisticsSource.Close,
        reversal_mode: int = ReversalMode.TrailingStopPosition,
    ):
        self.statistics_window_size = statistics_window_size
        self.mean_darvas_window_size = mean_darvas_window_size
        self.mean_hma_atr_smooth_window_size = mean_hma_atr_smooth_window_size
        self.statistics_source = statistics_source
        self.reversal_mode = reversal_mode


class _BaseEngine:
    """Base class for the four reference-mode engine wrappers.

    Each subclass sets ``_reference_type`` to the appropriate
    ``ReferenceType`` enum value. The base class handles loading the
    .NET runtime, constructing the engine via
    ``PythonEngineFactory``, and driving the pipeline.
    """

    _reference_type: int

    def __init__(
        self,
        market_data: Any,
        reference_kwargs: Optional[dict] = None,
        options: Optional[EngineOptions] = None,
    ) -> None:
        from research_feature_engine import _load_dotnet
        _load_dotnet()

        self._reference_kwargs = reference_kwargs or {}
        self._options = options or EngineOptions()

        # Extract the .NET IMarketData instance from the MarketData wrapper
        if hasattr(market_data, "instance"):
            self._market_data = market_data.instance
        else:
            self._market_data = market_data

        self._build_engine()

    def _build_engine(self) -> None:
        """Use PythonEngineFactory.Create to build the .NET engine."""
        from research_feature_engine import _get_type
        import System

        factory = _get_type(
            "ResearchFeatureEngine.Adapters.PythonEngineFactory",
            "ResearchFeatureEngine.Python",
        )
        create_method = factory.GetMethod("Create")

        # Build enum values
        ref_type = System.Enum.ToObject(
            self._ref_type_enum_type, self._reference_type
        )
        stats_source = System.Enum.ToObject(
            self._stats_source_enum_type, self._options.statistics_source
        )
        reversal_mode = System.Enum.ToObject(
            self._reversal_mode_enum_type, self._options.reversal_mode
        )

        self._engine = create_method.Invoke(None, [
            self._market_data,
            ref_type,
            System.Int32(self._reference_kwargs.get("atr_period", 16)),
            System.Double(self._reference_kwargs.get("atr_multiplier", 5.1)),
            System.Int32(self._reference_kwargs.get("smooth_length", 100)),
            System.Int32(self._reference_kwargs.get("box_length", 5)),
            System.Int32(self._reference_kwargs.get("hma_period", 16)),
            System.Int32(14),  # scaleAtrPeriod (fixed default)
            System.Int32(self._options.statistics_window_size),
            System.Int32(self._options.mean_darvas_window_size),
            System.Int32(self._options.mean_hma_atr_smooth_window_size),
            stats_source,
            reversal_mode,
        ])

    # Cached .NET enum type lookups (lazy — defer until first use)
    @property
    def _ref_type_enum_type(self) -> Any:
        return self._get_clr_type_cached("ReferenceType")

    @property
    def _stats_source_enum_type(self) -> Any:
        return self._get_clr_type_cached("StatisticsSource")

    @property
    def _reversal_mode_enum_type(self) -> Any:
        return self._get_clr_type_cached("ReversalMode")

    @classmethod
    def _get_clr_type_cached(cls, type_name: str) -> Any:
        """Resolve a .NET enum type by simple name, cached on the class.

        Looks up the type in either the Core (``ResearchFeatureEngine``)
        or the Python adapter (``ResearchFeatureEngine.Python``) assembly,
        whichever contains it. Results are cached on the class.
        """
        cache_attr = f"_cached_{type_name}"
        if not hasattr(cls, cache_attr):
            from research_feature_engine import _get_type
            # Try Core first, then Python adapter
            for asm_name in ("ResearchFeatureEngine", "ResearchFeatureEngine.Python"):
                try:
                    t = _get_type(f"ResearchFeatureEngine.Core.{type_name}", asm_name)
                    setattr(cls, cache_attr, t)
                    break
                except TypeError:
                    continue
            else:
                raise TypeError(
                    f"Could not resolve .NET enum type: {type_name}"
                )
        return getattr(cls, cache_attr)

    @property
    def engine(self) -> Any:
        """The underlying .NET ``ResearchFeatureEngine`` instance."""
        return self._engine

    @property
    def values(self) -> Any:
        """The .NET ``EngineValues`` object (populated as the engine runs)."""
        return self._engine.Values

    def run(
        self,
        market_data: Optional["MarketData"] = None,
        as_dataframe: bool = True,
        as_dict_list: bool = False,
    ) -> Any:
        """Drive the engine over all bars and return results.

        Parameters
        ----------
        market_data : MarketData, optional
            If provided, rebuilds the engine with the new market data.
        as_dataframe : bool
            If True (default), returns a pandas DataFrame.
        as_dict_list : bool
            If True, returns a list of dicts (one per bar).

        Returns
        -------
        pandas.DataFrame or list[dict]
            Per-bar published EngineValues.
        """
        if market_data is not None:
            if hasattr(market_data, "instance"):
                self._market_data = market_data.instance
            else:
                self._market_data = market_data
            self._build_engine()

        results = []
        n = self._engine.Context.MarketData.Count
        for i in range(n):
            self._engine.Update()
            row = self._extract_values(i)
            results.append(row)

        if as_dict_list:
            return results
        if as_dataframe:
            import pandas as pd
            return pd.DataFrame(results)
        return results

    def process_at(self, index: int) -> dict:
        """Process a single bar at ``index`` without auto-advancing.

        Re-ticks the same bar (re-tick semantics: deterministic).
        """
        self._engine.ProcessAt(index)
        return self._extract_values(index)

    def _extract_values(self, index: int) -> dict:
        """Extract the current published EngineValues into a flat dict."""
        v = self._engine.Values
        row = {"index": index}

        # Reference
        row["reference_price"] = v.Reference.Price
        row["reference_regime"] = v.Reference.Regime

        # Distance
        row["directional_distance"] = v.Distance.DirectionalExtension
        row["absolute_distance"] = v.Distance.AbsoluteExtension

        # Scale
        row["scale"] = v.Scale.Scale

        # Normalization
        row["normalized_measurement"] = v.Normalization.NormalizedMeasurement

        # Reversal
        rev = v.Reversal
        row["reversal_bars_since"] = rev.BarsSinceReversal
        row["reversal_direction"] = int(rev.Direction) if rev.Direction is not None else 0
        row["is_reversal_bar"] = rev.IsReversalBar

        # Statistics
        stats = v.Statistics
        row["stats_observation_count"] = stats.ObservationCount
        row["mean"] = stats.Location.Mean
        row["median"] = stats.Location.Median
        row["variance"] = stats.Dispersion.Variance
        row["std_dev"] = stats.Dispersion.StandardDeviation
        row["mad"] = stats.Dispersion.MedianAbsoluteDeviation
        row["min"] = stats.Range.Minimum
        row["max"] = stats.Range.Maximum
        row["range"] = stats.Range.Range
        row["skewness"] = stats.Shape.Skewness
        row["kurtosis"] = stats.Shape.Kurtosis

        # Darvas Box Distance (only populated for DarvasBox mode)
        darvas = v.DarvasBoxDistance
        row["darvas_has_box"] = darvas.HasBox
        row["darvas_signed_closing_distance"] = darvas.SignedClosingDistance
        row["darvas_absolute_closing_distance"] = darvas.AbsoluteClosingDistance

        # Mean Darvas Closing Distance (only populated for DarvasBox mode)
        row["mean_darvas_signed_distance"] = v.MeanDarvasClosingDistance.MeanSignedDistance

        # Dual-reference HMA/ATRSmooth features (only populated for HmaAtrSmooth)
        row["mean_hma_atr_distance"] = v.MeanHmaAtrSmoothDistance.MeanSignedDistance
        row["hma_alignment"] = str(v.HmaPriceAtrSmoothAlignment.Alignment)
        row["hma_separation"] = v.HmaAtrSmoothSeparation.Separation
        row["hma_relative_close_position"] = v.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition

        # ATRSmooth Regime Segment (only for ATRSmooth-based modes)
        seg = v.AtrSmoothRegimeSegment
        row["segment_regime"] = str(seg.Regime)
        row["segment_id"] = seg.RegimeId
        row["segment_start_index"] = seg.RegimeStartIndex
        row["segment_age"] = seg.RegimeAge
        row["segment_transition"] = str(seg.RegimeTransition)

        return row


class ATRSmooth2(_BaseEngine):
    """Engine using the ATRSmooth2 reference source.

    Reference = (VWMA(close, smooth) + ATR trailing-stop) / 2
    Regime = trailing-stop position bias (+1 bullish, -1 bearish, 0 initial).

    Parameters
    ----------
    market_data : MarketData
        The market data to process.
    atr_period : int, default 16
        ATR lookback period.
    atr_multiplier : float, default 5.1
        ATR multiplier for the trailing stop loss.
    smooth_length : int, default 100
        VWMA smoothing window length.
    options : EngineOptions, optional
        Advanced engine options.
    """

    _reference_type = ReferenceType.ATRSmooth2

    def __init__(
        self,
        market_data: Any,
        atr_period: int = 16,
        atr_multiplier: float = 5.1,
        smooth_length: int = 100,
        options: Optional[EngineOptions] = None,
    ) -> None:
        super().__init__(
            market_data,
            reference_kwargs={
                "atr_period": atr_period,
                "atr_multiplier": atr_multiplier,
                "smooth_length": smooth_length,
            },
            options=options,
        )


class DarvasBox(_BaseEngine):
    """Engine using the Darvas Box reference source.

    Reference = (Upper + Lower) / 2 of the current Darvas box.
    Regime = positional state (+1 above upper, 0 inside, -1 below lower).

    Parameters
    ----------
    market_data : MarketData
        The market data to process.
    box_length : int, default 5
        Darvas box length (bars). Minimum 3.
    options : EngineOptions, optional
        Advanced engine options.
    """

    _reference_type = ReferenceType.DarvasBox

    def __init__(
        self,
        market_data: Any,
        box_length: int = 5,
        options: Optional[EngineOptions] = None,
    ) -> None:
        super().__init__(
            market_data,
            reference_kwargs={"box_length": box_length},
            options=options,
        )


class Hma(_BaseEngine):
    """Engine using the Hull Moving Average reference source.

    Reference = HMA of close.
    Regime = HMA slope direction (+1 rising, -1 falling, 0 flat).

    Parameters
    ----------
    market_data : MarketData
        The market data to process.
    period : int, default 16
        HMA period (bars). Minimum 2.
    options : EngineOptions, optional
        Advanced engine options.
    """

    _reference_type = ReferenceType.Hma

    def __init__(
        self,
        market_data: Any,
        period: int = 16,
        options: Optional[EngineOptions] = None,
    ) -> None:
        super().__init__(
            market_data,
            reference_kwargs={"hma_period": period},
            options=options,
        )


class HmaAtrSmooth(_BaseEngine):
    """Composite dual-reference engine (HMA + ATRSmooth).

    The pipeline measurement level and regime are the ATRSmooth2
    equilibrium (identical to ATRSmooth2 mode), with the canonical
    HMA exposed in parallel as a research feature input.

    Parameters
    ----------
    market_data : MarketData
        The market data to process.
    atr_period : int, default 16
        ATR lookback period for the ATRSmooth component.
    atr_multiplier : float, default 5.1
        ATR multiplier for the trailing stop.
    smooth_length : int, default 100
        VWMA smoothing window for the ATRSmooth component.
    hma_period : int, default 16
        HMA period.
    options : EngineOptions, optional
        Advanced engine options.
    """

    _reference_type = ReferenceType.HmaAtrSmooth

    def __init__(
        self,
        market_data: Any,
        atr_period: int = 16,
        atr_multiplier: float = 5.1,
        smooth_length: int = 100,
        hma_period: int = 16,
        options: Optional[EngineOptions] = None,
    ) -> None:
        super().__init__(
            market_data,
            reference_kwargs={
                "atr_period": atr_period,
                "atr_multiplier": atr_multiplier,
                "smooth_length": smooth_length,
                "hma_period": hma_period,
            },
            options=options,
        )
