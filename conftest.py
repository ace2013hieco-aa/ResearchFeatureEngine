"""Pytest configuration: auto-set pythonnet runtime and path."""
import os
import sys
from pathlib import Path

# Force coreclr so pythonnet loads .NET 6+, not .NET Framework.
os.environ.setdefault("PYTHONNET_RUNTIME", "coreclr")

# Ensure repo root is importable when tests run from repo root.
ROOT = Path(__file__).resolve().parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

# Ensure DLLs are built before tests run.
DATA_DIR = ROOT / "research_feature_engine" / "data"
if not any(DATA_DIR.glob("*.dll")):
    import subprocess
    subprocess.run(
        [sys.executable, "build_dlls.py"],
        cwd=str(ROOT),
        check=True,
        capture_output=True,
    )
