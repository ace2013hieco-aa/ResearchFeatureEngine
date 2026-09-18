#!/usr/bin/env python3
"""Build script: compiles the .NET 6 core + Python adapter DLLs and
copies them into research_feature_engine/data/ so the wheel ships
them as package data.

Called by the GitHub Actions publish-pypi workflow before
``pip install .`` / ``pip wheel``.

Usage::

    python build_dlls.py            # build Release
    python build_dlls.py --debug    # build Debug
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
DATA_DIR = ROOT / "research_feature_engine" / "data"

CORE_CSPROJ = ROOT / "ResearchFeatureEngine.csproj"
PY_ADAPTER_CSPROJ = ROOT / "Adapters" / "ResearchFeatureEngine.Python" / "ResearchFeatureEngine.Python.csproj"

DLLS = ["ResearchFeatureEngine.dll", "ResearchFeatureEngine.Python.dll"]


def _copy_with_replace(src: Path, dst: Path) -> None:
    """Copy *src* to *dst*, replacing a locked destination on Windows."""
    try:
        shutil.copy2(src, dst)
    except PermissionError:
        # File is locked by a running process — try force-replacing
        # via the Windows API (MoveFileEx with MOVEFILE_REPLACE_EXISTING).
        import ctypes
        _MOVEFILE_REPLACE_EXISTING = 1
        _MOVEFILE_COPY_ALLOWED = 2
        ctypes.windll.kernel32.MoveFileExW(
            str(src),
            str(dst),
            _MOVEFILE_REPLACE_EXISTING | _MOVEFILE_COPY_ALLOWED,
        )


def main() -> None:
    config = "Debug" if "--debug" in sys.argv else "Release"
    print(f"[build_dlls] Configuration: {config}")

    if shutil.which("dotnet") is None:
        print("[build_dlls] ERROR: dotnet CLI not found.")
        sys.exit(1)

    # Restore + build Core
    print("[build_dlls] Building Core ...")
    subprocess.run(
        ["dotnet", "build", str(CORE_CSPROJ), "-c", config, "--nologo", "--no-restore"],
        cwd=ROOT,
        check=True,
    )

    # Restore + build Python adapter
    print("[build_dlls] Building Python adapter ...")
    subprocess.run(
        ["dotnet", "build", str(PY_ADAPTER_CSPROJ), "-c", config, "--nologo", "--no-restore"],
        cwd=ROOT,
        check=True,
    )

    # The Python adapter's csproj sets OutputPath to ..\..\bin\$(Configuration)\
    # so both DLLs end up in the same shared bin directory.
    # Also check the default adapter output as a fallback.
    out_dirs = [
        ROOT / "bin" / config,                                                       # shared output (both DLLs)
        ROOT / "bin" / config / "net6.0",                                           # Core default output
        ROOT / "Adapters" / "ResearchFeatureEngine.Python" / "bin" / config,        # adapter fallback
    ]

    # Clean data dir — tolerate locked files (DLL may be in use by
    # a running Python process; we overwrite below).
    if DATA_DIR.exists():
        shutil.rmtree(DATA_DIR, ignore_errors=True)
    DATA_DIR.mkdir(parents=True, exist_ok=True)

    # Copy the two required DLLs
    for dll in DLLS:
        src = None
        for d in out_dirs:
            candidate = d / dll
            if candidate.exists():
                src = candidate
                break
        if src:
            dst = DATA_DIR / dll
            try:
                _copy_with_replace(src, dst)
            except Exception as e:
                print(f"  WARNING: Could not copy {dll}: {e}")
                print(f"  (The DLL may be in use. Close any Python processes "
                      f"that have imported research_feature_engine.)")
            print(f"  Copied {dll} from {src}")
        else:
            print(f"  MISSING: {dll}")
            print(f"    Searched: {[str(d / dll) for d in out_dirs]}")
            sys.exit(1)

    print(f"[build_dlls] Done. DLLs at {DATA_DIR}")


if __name__ == "__main__":
    main()
