"""FIX QUOTE tick recorder — live symbols, 25-tick bar aggregation.

Charter (inherited from fix_quote harness): QUOTE session only — market
data, NEVER order routing (no 35=D). Password is loaded by fix_quote_client
from the user's env file and never printed; no raw FIX messages are
written, so all outputs are redacted by construction.

Outputs in --outdir (must not already exist):
  run.log             status / reconnect / summary lines (UTC)
  ticks_<SYM>.csv     one row per tick event:
                      ts_recv_utc,bid,ask,spread,msg_seq
  bars_<SYM>_t25.csv  one row per 25-tick bar:
                      OpenTimeUtc,Open,High,Low,Close,TickVolume,Spread

Definitions (deliberate, documented):
- tick event = one 35=W/35=X application message touching the symbol with
  >=1 applied bid/ask price update (mirrors a cTrader tick: one bid/ask
  pair per event; a message carrying both a bid and an ask update is ONE
  tick, not two).
- bar OHLC is BID-based (matches MarketDataRecorder convention);
  TickVolume = counted tick events (target 25); Spread = ask - bid in
  price units at the bar's LAST tick.
- timestamps are RECEIVE-time UTC ms (harness convention), not broker 52.
- a trailing partial bar at stop is flushed with its true TickVolume
  (<25), like the recorder's session-start partial bars.

Stop paths:
- --until HH:MM (local clock) deadline -> graceful logout + partial flush.
- create the file STOP inside --outdir -> clean early stop (checked at
  least every 2 s) with the same graceful flush.
Reconnects: on transport errors the FIX session is rebuilt and
resubscribed until the deadline (15 s backoff); per-symbol tick/bar state
survives across reconnects.
"""
from __future__ import annotations

import argparse
import csv
import json
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Dict, List, Optional

import sys

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from fix_quote_client import (  # noqa: E402
    SYMBOL_CACHE,
    FixCredentials,
    QuoteClient,
    load_credentials,
    parse_md_incremental,
    parse_md_snapshot,
)

TICK_HEADER = ["ts_recv_utc", "bid", "ask", "spread", "msg_seq"]
BAR_HEADER = ["OpenTimeUtc", "Open", "High", "Low", "Close", "TickVolume", "Spread"]


class FatalError(RuntimeError):
    """Unrecoverable — stop the run (bad credentials, rejected request)."""


def log(run_log: Path, msg: str) -> None:
    line = f"{datetime.now(timezone.utc).isoformat(timespec='milliseconds')} {msg}"
    print(line, flush=True)
    with run_log.open("a", encoding="utf-8") as f:
        f.write(line + "\n")


def utc_now_ms() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds")


class SymState:
    """Per-symbol tick/bar accumulator writing to flushed CSVs."""

    def __init__(self, name: str, symbol_id: int, digits: int, outdir: Path):
        self.name = name
        self.symbol_id = symbol_id
        self.digits = digits
        self.tick_f = (outdir / f"ticks_{name}.csv").open(
            "w", newline="", encoding="utf-8")
        self.bar_f = (outdir / f"bars_{name}_t25.csv").open(
            "w", newline="", encoding="utf-8")
        self.tick_w = csv.writer(self.tick_f)
        self.bar_w = csv.writer(self.bar_f)
        self.tick_w.writerow(TICK_HEADER)
        self.bar_w.writerow(BAR_HEADER)
        self.bid: Optional[float] = None
        self.ask: Optional[float] = None
        self.ticks = 0
        self.bars = 0
        self.bar_n = 0
        self.bar_open_ts: Optional[str] = None
        self.o: Optional[float] = None
        self.h: Optional[float] = None
        self.l: Optional[float] = None
        self.c: Optional[float] = None
        self.last_spread: Optional[float] = None
        self.first_ts: Optional[str] = None
        self.last_ts: Optional[str] = None

    def apply_tick(self, ts: str, seq: int, updates: Dict[str, float]) -> None:
        if "bid" in updates:
            self.bid = updates["bid"]
        if "ask" in updates:
            self.ask = updates["ask"]
        if self.bid is None or self.ask is None:
            return  # need both sides before counting a tick
        spread = round(self.ask - self.bid, 6)
        self.last_spread = spread
        self.ticks += 1
        self.first_ts = self.first_ts or ts
        self.last_ts = ts
        self.tick_w.writerow([ts, self.bid, self.ask, spread, seq])
        self.tick_f.flush()
        if self.bar_n == 0:
            self.bar_open_ts = ts
            self.o = self.h = self.l = self.c = self.bid
        else:
            if self.bid > self.h:
                self.h = self.bid
            if self.bid < self.l:
                self.l = self.bid
            self.c = self.bid
        self.bar_n += 1
        if self.bar_n >= 25:
            self.flush_bar()

    def flush_bar(self) -> None:
        if self.bar_n == 0:
            return
        self.bar_w.writerow(
            [self.bar_open_ts, self.o, self.h, self.l, self.c,
             self.bar_n, self.last_spread])
        self.bar_f.flush()
        self.bars += 1
        self.bar_n = 0
        self.bar_open_ts = None
        self.o: Optional[float] = None
        self.h: Optional[float] = None
        self.l: Optional[float] = None
        self.c: Optional[float] = None

    def close(self) -> None:
        self.flush_bar()  # partial bar keeps its true TickVolume
        self.tick_f.close()
        self.bar_f.close()


def open_session(creds: FixCredentials) -> QuoteClient:
    client = QuoteClient(creds)
    client.session.connect()
    try:
        client.session.logon()
    except ConnectionError as e:
        if "rejected" in str(e):
            raise FatalError(f"logon rejected: {e}") from e
        raise  # transport-level: caller retries
    return client


def resolve_symbols(client: QuoteClient, wanted: List[str]) -> Dict[str, tuple]:
    """Live SecurityList, falling back to the on-disk cache."""
    try:
        return client.fetch_symbols()
    except Exception as e:  # transport or timeout — cache ids are stable
        if not SYMBOL_CACHE.exists():
            raise FatalError(
                f"symbol fetch failed ({e}) and no cache at {SYMBOL_CACHE}")
        cached = json.loads(SYMBOL_CACHE.read_text(encoding="utf-8"))
        return {k: tuple(v) for k, v in cached.items()}


def entries_to_updates(entries) -> Optional[Dict[str, float]]:
    """Filter quote entries to {side: price}; None if nothing applicable."""
    updates: Dict[str, float] = {}
    for e in entries:
        if e.side not in ("bid", "ask") or not e.price:
            continue
        try:
            updates[e.side] = float(e.price)
        except ValueError:
            continue
    return updates or None


def parse_args(argv: Optional[List[str]] = None):
    p = argparse.ArgumentParser(description="FIX QUOTE tick -> 25-tick bar recorder")
    p.add_argument("--symbols", default="XAUUSD,USDJPY",
                   help="comma-separated symbol names")
    p.add_argument("--until", default="22:00",
                   help="stop at HH:MM local time today (24h clock)")
    p.add_argument("--outdir", required=True,
                   help="run output dir (created; must not exist)")
    p.add_argument("--bar-ticks", type=int, default=25,
                   help="ticks per bar (default 25)")
    p.add_argument("--cred-file", default=None)
    return p.parse_args(argv)


def run(args) -> int:
    outdir = Path(args.outdir)
    if outdir.exists():
        print(f"ERROR: outdir already exists: {outdir}", flush=True)
        return 2
    outdir.mkdir(parents=True)
    run_log = outdir / "run.log"
    stop_file = outdir / "STOP"

    wanted = [s.strip().upper() for s in args.symbols.split(",") if s.strip()]
    hour, minute = (int(x) for x in args.until.split(":"))
    now = datetime.now()
    end_local = now.replace(hour=hour, minute=minute, second=0, microsecond=0)
    if end_local <= now:
        end_local += timedelta(days=1)
    duration_s = (end_local - now).total_seconds()
    if duration_s > 12 * 3600:
        print(f"ERROR: duration {duration_s/3600:.1f}h > 12h guard "
              f"(--until {args.until}) — refusing", flush=True)
        return 2

    creds = load_credentials(Path(args.cred_file)) if args.cred_file \
        else load_credentials()
    log(run_log, f"run dir: {outdir}")
    log(run_log, f"deadline: {end_local.isoformat()} local = "
                 f"{end_local.astimezone(timezone.utc).isoformat()} | "
                 f"duration: {duration_s:.0f}s")
    log(run_log, f"symbols: {wanted} | bar size: {args.bar_ticks} ticks")

    deadline = time.monotonic() + duration_s
    states: Dict[int, SymState] = {}      # keyed by FIX symbol id
    symbols: Dict[str, tuple] = {}
    reconnects = 0
    client: Optional[QuoteClient] = None
    last_status = time.monotonic()

    log(run_log, "connecting (TLS + logon)...")
    try:
        while time.monotonic() < deadline and not stop_file.exists():
            try:
                client = open_session(creds)
                log(run_log, "logon accepted")
                if not symbols:
                    symbols = resolve_symbols(client, wanted)
                missing = [s for s in wanted if s not in symbols]
                if missing:
                    raise FatalError(f"symbols not offered: {missing}")
                if not states:
                    for s in wanted:
                        sym_id, digits = symbols[s]
                        states[sym_id] = SymState(s, sym_id, digits, outdir)
                        log(run_log, f"target: {s} id={sym_id} digits={digits}")
                client.subscribe(list(states.keys()))
                log(run_log, "subscribed: " + ", ".join(
                    f"{st.name}={sid}" for sid, st in states.items()))

                while time.monotonic() < deadline and not stop_file.exists():
                    if (time.monotonic() - client.session.last_send
                            > client.session.heartbeat_interval - 5):
                        client.session.heartbeat()
                    msg = client.session.recv(timeout=2.0)
                    if msg is None:
                        continue
                    if msg.msg_type in ("0", "1", "2", "4"):
                        client.session.handle_heartbeat_duties(msg)
                        continue
                    if msg.msg_type == "5":
                        log(run_log, f"server Logout: {msg.get(58, '')}")
                        raise ConnectionError("server-initiated logout")
                    if msg.msg_type == "Y":
                        raise FatalError(
                            f"MDRequestReject: {msg.get(58, '<no text>')}")
                    if msg.msg_type == "3":
                        log(run_log, f"Reject (35=3): {msg.get(58, '')}")
                        continue
                    ts = utc_now_ms()
                    if msg.msg_type == "W":
                        sym_id, entries = parse_md_snapshot(msg)
                        st = states.get(sym_id)
                        if st:
                            updates = entries_to_updates(entries)
                            if updates:
                                st.apply_tick(ts, client.session.seq, updates)
                    elif msg.msg_type == "X":
                        per_sym: Dict[int, list] = {}
                        for e in parse_md_incremental(msg):
                            if e.symbol_id in states:
                                per_sym.setdefault(e.symbol_id, []).append(e)
                        for sid, group in per_sym.items():
                            updates = entries_to_updates(group)
                            if updates:
                                states[sid].apply_tick(ts, client.session.seq,
                                                       updates)
                    if time.monotonic() - last_status > 300:
                        last_status = time.monotonic()
                        log(run_log, "status: " + "; ".join(
                            f"{st.name} ticks={st.ticks} bars={st.bars}"
                            for st in states.values()))
            except FatalError as e:
                log(run_log, f"FATAL: {e}")
                break
            except Exception as e:  # transport-level: rebuild and retry
                reconnects += 1
                remaining = deadline - time.monotonic()
                log(run_log, f"transport error ({type(e).__name__}: {e}); "
                             f"reconnect #{reconnects} in 15s "
                             f"({max(0.0, remaining):.0f}s left)")
                if client:
                    try:
                        client.session.close()
                    except Exception:
                        pass
                if remaining > 0:
                    time.sleep(min(15.0, remaining))
    finally:
        if stop_file.exists():
            log(run_log, "STOP file detected — clean early stop")
        for st in states.values():
            st.close()
        if client:
            try:
                client.session.logout()
            except Exception:
                pass
            try:
                client.session.close()
            except Exception:
                pass
        wall = duration_s - max(0.0, deadline - time.monotonic())
        log(run_log, f"summary: wall={wall:.0f}s reconnects={reconnects}")
        for st in states.values():
            log(run_log, f"final {st.name}: ticks={st.ticks} bars={st.bars} "
                         f"first={st.first_ts} last={st.last_ts}")
        log(run_log, "DONE")
    return 0


if __name__ == "__main__":
    sys.exit(run(parse_args()))
