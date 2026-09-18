"""
research-feature-engine
=======================

A pythonnet-based Python binding to the ResearchFeatureEngine .NET 6
core engine. Provides Pythonic access to the Reference → Distance →
Scale → Normalization → Statistics pipeline with four reference modes:

- ``ATRSmooth2`` — equilibrium-level reference (VWMA + ATR trailing stop)
- ``DarvasBox``   — box-midpoint reference
- ``Hma``         — Hull Moving Average reference
- ``HmaAtrSmooth`` — composite dual-reference (HMA + ATRSmooth)

Usage::

    from research_feature_engine import HmaAtrSmooth, MarketData

    md = MarketData.from_csv("EURUSD_M1_10000.csv")
    engine = HmaAtrSmooth(md)
    df = engine.run()
    print(df.columns)
"""

import os
import sys
from pathlib import Path
from typing import Any, Optional

__version__ = "0.2.0"

# Force coreclr runtime at import time so pythonnet loads .NET 6+
# (not .NET Framework) regardless of whether the user set the env var.
os.environ.setdefault("PYTHONNET_RUNTIME", "coreclr")

_clr_loaded = False
_clr_error: Optional[Exception] = None
_data_dir: Optional[Path] = None
_assemblies_loaded = False

try:
    import clr
    _HAS_PYTHONNET = True
except ImportError as exc:
    _HAS_PYTHONNET = False
    _clr_error = exc

# Expose submodules
from research_feature_engine.market_data import MarketData, load_market_data
from research_feature_engine.engine import (
    ReferenceType,
    StatisticsSource,
    ReversalMode,
    EngineOptions,
    ATRSmooth2,
    DarvasBox,
    Hma,
    HmaAtrSmooth,
)


def _is_windows() -> bool:
    return sys.platform == "win32"


def _get_data_dir() -> Path:
    return Path(__file__).resolve().parent / "data"


def _load_dotnet() -> None:
    """Load the .NET runtime via pythonnet and register the built DLLs.

    Uses the ``coreclr`` runtime (required for .NET 6+ assemblies).
    Loads both ``ResearchFeatureEngine.dll`` (Core) and
    ``ResearchFeatureEngine.Python.dll`` (adapter) from the package's
    ``data/`` directory.
    """
    global _clr_loaded, _clr_error, _data_dir, _assemblies_loaded

    if _clr_loaded:
        return

    if not _HAS_PYTHONNET:
        raise ImportError(
            "pythonnet is required but not installed. "
            "Install with: pip install pythonnet>=3.0"
        ) from _clr_error

    # Force coreclr runtime (not .NET Framework)
    os.environ["PYTHONNET_RUNTIME"] = "coreclr"

    data_dir = _get_data_dir()
    _data_dir = data_dir

    if not data_dir.exists():
        raise FileNotFoundError(
            f" .NET assembly data directory not found: {data_dir}. "
            f"Run 'python build_dlls.py' to build the DLLs."
        )

    # Ensure the data directory is on the native library search path
    if _is_windows():
        os.environ["PATH"] = (
            str(data_dir) + os.pathsep + os.environ.get("PATH", "")
        )

    import clr
    import System

    # Load assemblies via Assembly.LoadFrom so System.Type.GetType
    # can resolve them later. Assembly.LoadFrom registers the assembly
    # in the current AppDomain, making Type.GetType work.
    core_asm_path = str(data_dir / "ResearchFeatureEngine.dll")
    py_asm_path = str(data_dir / "ResearchFeatureEngine.Python.dll")

    System.Reflection.Assembly.LoadFrom(core_asm_path)
    System.Reflection.Assembly.LoadFrom(py_asm_path)

    _assemblies_loaded = True
    _clr_loaded = True


def _get_type(full_name: str, assembly_name: str = "ResearchFeatureEngine") -> Any:
    """Resolve a .NET type by full name.

    Parameters
    ----------
    full_name : str
        The full type name, e.g.
        ``ResearchFeatureEngine.Adapters.PythonMarketData``.
    assembly_name : str
        The short assembly name (without extension), e.g.
        ``ResearchFeatureEngine.Python`` or ``ResearchFeatureEngine``.
    """
    if not _clr_loaded:
        _load_dotnet()

    import System
    type = System.Type.GetType(f"{full_name}, {assembly_name}")
    if type is None:
        raise TypeError(
            f"Could not resolve .NET type: {full_name} in assembly {assembly_name}"
        )
    return type


def is_loaded() -> bool:
    """Return True if the .NET runtime and assemblies are loaded."""
    return _clr_loaded


def load_dotnet() -> None:
    """Public entry point to force .NET runtime + assembly loading."""
    _load_dotnet()


def get_data_dir() -> Path:
    """Return the directory containing the bundled .NET DLLs."""
    return _get_data_dir()


__all__ = [
    "__version__",
    "ReferenceType",
    "StatisticsSource",
    "ReversalMode",
    "EngineOptions",
    "ATRSmooth2",
    "DarvasBox",
    "Hma",
    "HmaAtrSmooth",
    "MarketData",
    "load_market_data",
    "load_dotnet",
    "is_loaded",
    "get_data_dir",
]
