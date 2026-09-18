"""cTrader FIX 4.4 QUOTE-session client - market data only.

Wire grammar from help.ctrader.com/fix/specification (fetched 2026-09-02,
saved as tools/_ctrader_fix_spec.txt):
- SOH (0x01) separates fields; '|' in docs is prose only.
- Logon 35=A: 98=0, 108=30, 141=Y, 553=<numeric login>, 554=<password>.
- MarketDataRequest 35=V: 262 reqID, 263=1 snapshot+updates, 264/265 depth,
  146 NoRelatedSym, 55=<numeric symbol id>, 267 NoMDEntries, 269=0 bid / 1 ask.
- SecurityListRequest 35=x / response 35=y maps symbol name (1007) to id (55).
- Quotes arrive as 35=W (full snapshot) then 35=X (incremental).

Security: password is loaded at runtime from the user's private env file;
never printed, logged, or echoed. __repr__ of credentials redacts tag 554;
`redact_message()` strips 554 from any raw FIX string before display.
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import random
import socket
import ssl
import sys
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Tuple

SOH = "\x01"
SOH_B = b"\x01"
BEGIN_STRING = "FIX.4.4"

HERE = Path(__file__).resolve().parent
DEFAULT_CRED_FILE = Path(
    os.environ.get(
        "CTRADER_FIX_CRED_FILE",
        r"C:/Users/Ali Zoghi/AppData/Local/hermes/ctrader-API.env",
    )
)
SYMBOL_CACHE = HERE / "symbol_cache.json"


# ── credentials ──────────────────────────────────────────────────────
@dataclass
class FixCredentials:
    host: str
    port: int
    username: str        # tag 553 (numeric trader login)
    password: str        # tag 554 - NEVER displayed
    sender_comp_id: str  # tag 49
    target_comp_id: str  # tag 56
    sender_sub_id: str   # tag 50/57 session qualifier (QUOTE)

    def __repr__(self) -> str:
        return (
            f"FixCredentials(host={self.host!r}, port={self.port}, "
            f"username={self.username!r}, password=<redacted>, "
            f"sender_comp_id={self.sender_comp_id!r}, "
            f"target_comp_id={self.target_comp_id!r}, "
            f"sender_sub_id={self.sender_sub_id!r})"
        )


def load_credentials(env_file: Path = DEFAULT_CRED_FILE) -> FixCredentials:
    """Read CTRADER_FIX_PASS from env_file; env vars override the rest."""
    env_file = Path(env_file)
    if not env_file.exists():
        raise FileNotFoundError(
            f"credential file not found: {env_file}; "
            "expected one line: CTRADER_FIX_PASS=<password>"
        )
    key = "CTRADER_FIX_PASS"
    value = None
    for line in env_file.read_text(encoding="utf-8-sig").splitlines():
        line = line.strip()
        if line.startswith(key + "="):
            value = line.split("=", 1)[1].strip()
            break
    if not value:
        raise ValueError(f"{key} missing/empty in {env_file}")
    return FixCredentials(
        host=os.environ.get("CTRADER_FIX_HOST", "live-us-eqx-02.p.c-trader.com"),
        port=int(os.environ.get("CTRADER_FIX_PORT", "5211")),
        username=os.environ.get("CTRADER_FIX_USERNAME", "1160380"),
        password=value,
        sender_comp_id=os.environ.get(
            "CTRADER_FIX_SENDER", "live.pepperstone.1160380"
        ),
        target_comp_id=os.environ.get("CTRADER_FIX_TARGET", "cServer"),
        sender_sub_id=os.environ.get("CTRADER_FIX_SUBID", "QUOTE"),
    )


# ── message construction ────────────────────────────────────────────
def fix_timestamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%d-%H:%M:%S.%f")[:-3]


def build_message(
    creds: FixCredentials,
    seq: int,
    msg_type: str,
    body_pairs: List[Tuple[int, str]],
    include_sub_ids: bool = True,
) -> str:
    """Build one FIX message with correct BodyLength (9) and Checksum (10)."""
    header: List[Tuple[int, str]] = [
        (35, msg_type),
        (49, creds.sender_comp_id),
        (56, creds.target_comp_id),
        (34, str(seq)),
        (52, fix_timestamp()),
    ]
    if include_sub_ids:
        header += [(50, creds.sender_sub_id), (57, creds.sender_sub_id)]
    pairs = header + list(body_pairs)
    body = SOH.join(f"{tag}={val}" for tag, val in pairs) + SOH
    body_bytes = body.encode("ascii")
    prefix = f"8={BEGIN_STRING}{SOH}9={len(body_bytes)}{SOH}".encode("ascii")
    without_checksum = prefix + body_bytes
    checksum = sum(without_checksum) % 256
    return (
        without_checksum.decode("ascii") + f"10={checksum:03d}" + SOH
    )


@dataclass
class FixMessage:
    seq: int
    msg_type: str
    tags: Dict[int, str]          # first occurrence of each tag
    pairs: List[Tuple[int, str]]  # all tags in order (for repeating groups)

    def get(self, tag: int, default: Optional[str] = None) -> Optional[str]:
        return self.tags.get(tag, default)

    def __repr__(self) -> str:
        return f"FixMessage(35={self.msg_type}, 34={self.seq}, tags={self.tags})"


def parse_message(raw: str) -> FixMessage:
    parts = [p for p in raw.split(SOH) if p]
    pairs: List[Tuple[int, str]] = []
    for p in parts:
        tag, _, val = p.partition("=")
        if tag.lstrip("-").isdigit():
            pairs.append((int(tag), val))
    tags: Dict[int, str] = {}
    for tag, val in pairs:
        tags.setdefault(tag, val)
    return FixMessage(
        seq=int(tags.get(34, 0)),
        msg_type=tags.get(35, "?"),
        tags=tags,
        pairs=pairs,
    )


def redact_message(raw: str) -> str:
    """Replace tag 554 (Password) value in a raw FIX string before display."""
    import re
    return re.sub(r"554=[^\x01]*", "554=<redacted>", raw)


# ── session ──────────────────────────────────────────────────────────
class FixSession:
    def __init__(self, creds: FixCredentials, verbose: bool = False):
        self.creds = creds
        self.verbose = verbose
        self.seq = 0
        self.buffer = b""
        self.sock: Optional[ssl.SSLSocket] = None
        self.last_send = time.monotonic()
        self.last_recv = time.monotonic()
        self.heartbeat_interval = 30

    # -- transport --
    def connect(self) -> None:
        raw = socket.create_connection((self.creds.host, self.creds.port), timeout=15)
        ctx = ssl.create_default_context()
        self.sock = ctx.wrap_socket(raw, server_hostname=self.creds.host)
        self.sock.settimeout(30)
        if self.verbose:
            print(f"[session] TLS connected {self.creds.host}:{self.creds.port}")

    def close(self) -> None:
        if self.sock:
            try:
                self.sock.close()
            except OSError:
                pass
            self.sock = None

    def send(self, msg_type: str, body_pairs: List[Tuple[int, str]]) -> int:
        self.seq += 1
        raw = build_message(self.creds, self.seq, msg_type, body_pairs)
        assert self.sock is not None
        self.sock.sendall(raw.encode("ascii"))
        self.last_send = time.monotonic()
        if self.verbose:
            print(f">>> {redact_message(raw.rstrip(SOH)).replace(SOH, '|')}")
        return self.seq

    def _frame(self) -> Optional[str]:
        """Return one complete message from the buffer, else None."""
        marker = b"\x0110="
        idx = self.buffer.find(marker)
        while idx != -1:
            end = idx + len(marker) + 3  # 3 checksum digits
            if len(self.buffer) >= end + 1 and self.buffer[end : end + 1] == SOH_B:
                msg = self.buffer[: end + 1]
                self.buffer = self.buffer[end + 1 :]
                return msg.decode("ascii", errors="replace")
            idx = self.buffer.find(marker, idx + 1)
        return None

    def recv(self, timeout: float = 5.0) -> Optional[FixMessage]:
        assert self.sock is not None
        deadline = time.monotonic() + timeout
        while True:
            msg = self._frame()
            if msg is not None:
                self.last_recv = time.monotonic()
                return parse_message(msg)
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                return None
            self.sock.settimeout(min(remaining, 5.0))
            try:
                chunk = self.sock.recv(8192)
            except socket.timeout:
                continue
            if not chunk:
                raise ConnectionError("server closed the connection")
            self.buffer += chunk

    # -- session level --
    def logon(self, timeout: float = 10.0) -> FixMessage:
        self.send("A", [
            (98, "0"),
            (108, str(self.heartbeat_interval)),
            (141, "Y"),
            (553, self.creds.username),
            (554, self.creds.password),
        ])
        msg = self.recv(timeout)
        if msg is None:
            raise TimeoutError("no Logon response")
        if msg.msg_type != "A":
            raise ConnectionError(
                f"logon rejected: 35={msg.msg_type} "
                f"text={msg.get(58, '<none>')}"
            )
        return msg

    def logout(self) -> None:
        try:
            self.send("5", [])
        except OSError:
            pass

    def heartbeat(self) -> None:
        self.send("0", [])

    def handle_heartbeat_duties(self, msg: FixMessage) -> None:
        """Respond to TestRequest; track server Logout."""
        if msg.msg_type == "1":  # TestRequest -> echo Heartbeat with 112
            self.send("0", [(112, msg.get(112, ""))])
        elif msg.msg_type == "2":  # ResendRequest -> gap-fill reset
            self.send("4", [(123, "Y"), (36, str(self.seq + 1))])


# ── application level ────────────────────────────────────────────────
def parse_security_list(msg: FixMessage) -> Dict[str, Tuple[int, int]]:
    """35=y -> {symbolName: (symbolId, digits)} from repeating 55/1007/1008."""
    result: Dict[str, Tuple[int, int]] = {}
    cur_id: Optional[int] = None
    for tag, val in msg.pairs:
        if tag == 55:
            cur_id = int(val)
        elif tag == 1007 and cur_id is not None:
            result[val] = (cur_id, -1)
    for tag, val in msg.pairs:  # second pass for digits (order per docs: 55,1007,1008)
        pass
    # digits ride in the same group right after 1007; walk pairs again
    last_name: Optional[str] = None
    for tag, val in msg.pairs:
        if tag == 1007:
            last_name = val
        elif tag == 1008 and last_name is not None and last_name in result:
            sym_id, _ = result[last_name]
            result[last_name] = (sym_id, int(val))
    return result


@dataclass
class QuoteEntry:
    symbol_id: int
    side: str        # 'bid' | 'ask'
    price: str
    size: str = ""
    entry_id: str = ""
    update: str = "snapshot"  # snapshot | new | change | delete


def parse_md_snapshot(msg: FixMessage) -> Tuple[int, List[QuoteEntry]]:
    """35=W MarketDataSnapshot -> (symbol_id, entries)."""
    symbol_id = int(msg.get(55, "0"))
    entries: List[QuoteEntry] = []
    side = price = size = ""
    started = False
    for tag, val in msg.pairs:
        if tag == 269:
            if started and side:
                entries.append(QuoteEntry(symbol_id, side, price, size))
            side = {"0": "bid", "1": "ask"}.get(val, val)
            price = ""
            size = ""
            started = True
        elif tag == 270 and started:
            price = val
        elif tag == 271 and started:
            size = val
    if started and side:
        entries.append(QuoteEntry(symbol_id, side, price, size))
    return symbol_id, entries


def parse_md_incremental(msg: FixMessage) -> List[QuoteEntry]:
    """35=X MarketDataIncremental -> entries with new/change/delete flags."""
    out: List[QuoteEntry] = []
    update = side = price = size = entry_id = ""
    symbol_id = 0
    started = False
    for tag, val in msg.pairs:
        if tag == 279:
            if started and side:
                out.append(QuoteEntry(symbol_id, side, price, size, entry_id, update))
            update = {"0": "new", "1": "change", "2": "delete"}.get(val, val)
            side = price = size = entry_id = ""
            started = True
        elif tag == 269 and started:
            side = {"0": "bid", "1": "ask"}.get(val, val)
        elif tag == 55 and started:
            # cTrader puts the symbol id INSIDE each entry group (after 278)
            symbol_id = int(val) if val.isdigit() else symbol_id
        elif tag == 270 and started:
            price = val
        elif tag == 271 and started:
            size = val
        elif tag == 278 and started:
            entry_id = val
    if started and side:
        out.append(QuoteEntry(symbol_id, side, price, size, entry_id, update))
    return out


class QuoteClient:
    """High-level client: logon, symbol map, subscribe, stream to CSV."""

    def __init__(self, creds: FixCredentials, verbose: bool = False):
        self.creds = creds
        self.session = FixSession(creds, verbose=verbose)
        self.symbols: Dict[str, Tuple[int, int]] = {}

    def fetch_symbols(self) -> Dict[str, Tuple[int, int]]:
        req_id = f"sym{int(time.time())}{random.randint(100, 999)}"
        self.session.send("x", [(320, req_id), (559, "0")])
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            msg = self.session.recv(timeout=deadline - time.monotonic())
            if msg is None:
                break
            self.session.handle_heartbeat_duties(msg)
            if msg.msg_type == "y":
                self.symbols = parse_security_list(msg)
                SYMBOL_CACHE.write_text(
                    json.dumps(
                        {k: v for k, v in self.symbols.items()}, indent=1, sort_keys=True
                    ),
                    encoding="utf-8",
                )
                return self.symbols
            if msg.msg_type == "3":  # Reject
                raise ConnectionError(f"security list rejected: {msg.get(58, '')}")
        raise TimeoutError("no SecurityList response")

    def subscribe(self, symbol_ids: List[int]) -> None:
        for sym_id in symbol_ids:
            body = [
                (262, f"md{sym_id}{int(time.time())}"),
                (263, "1"),   # snapshot + updates
                (264, "1"),   # top of book
                (265, "1"),   # update on each change
                (146, "1"),
                (55, str(sym_id)),
                (267, "2"),
                (269, "0"),
                (269, "1"),
            ]
            self.session.send("V", body)

    def stream(
        self,
        duration_s: float,
        id_to_name: Dict[int, str],
        csv_path: Optional[Path] = None,
        raw_path: Optional[Path] = None,
        quiet: bool = False,
    ) -> List[QuoteEntry]:
        rows: List[QuoteEntry] = []
        csv_file = None
        csv_writer = None
        if csv_path:
            csv_path.parent.mkdir(parents=True, exist_ok=True)
            csv_file = open(csv_path, "w", newline="", encoding="utf-8")
            csv_writer = csv.writer(csv_file)
            csv_writer.writerow(
                ["ts_utc", "symbol_id", "symbol", "side", "price", "size",
                 "entry_id", "update", "msg_seq"]
            )
        raw_file = open(raw_path, "a", encoding="utf-8") if raw_path else None
        deadline = time.monotonic() + duration_s
        rejected = False
        try:
            while time.monotonic() < deadline:
                if time.monotonic() - self.session.last_send > self.session.heartbeat_interval - 5:
                    self.session.heartbeat()
                msg = self.session.recv(timeout=2.0)
                if msg is None:
                    continue
                if raw_file:
                    raw_file.write(redact_message(msg.pairs and
                        SOH.join(f"{t}={v}" for t, v in msg.pairs) + SOH) + "\n")
                if msg.msg_type in ("0", "1", "2", "4"):
                    self.session.handle_heartbeat_duties(msg)
                    continue
                if msg.msg_type == "5":
                    if not quiet:
                        print(f"[stream] server Logout: {msg.get(58, '')}")
                    break
                if msg.msg_type == "Y":
                    if not quiet:
                        print(f"[stream] MDRequestReject: {msg.get(58, '')}")
                    rejected = True
                    break
                if msg.msg_type == "W":
                    _, entries = parse_md_snapshot(msg)
                    for e in entries:
                        rows.append(e)
                        self._emit(e, id_to_name, csv_writer, quiet)
                elif msg.msg_type == "X":
                    for e in parse_md_incremental(msg):
                        rows.append(e)
                        self._emit(e, id_to_name, csv_writer, quiet)
        finally:
            if raw_file:
                raw_file.close()
            if csv_file:
                csv_file.close()
        if rejected:
            raise ConnectionError("market data request rejected by server")
        return rows

    def _emit(self, e: QuoteEntry, id_to_name, csv_writer, quiet) -> None:
        ts = datetime.now(timezone.utc).isoformat(timespec="milliseconds")
        name = id_to_name.get(e.symbol_id, str(e.symbol_id))
        if csv_writer:
            csv_writer.writerow(
                [ts, e.symbol_id, name, e.side, e.price, e.size, e.entry_id,
                 e.update, self.session.seq]
            )
        if not quiet and e.update != "delete":
            print(f"{ts} {name:>8} {e.side:>3} {e.price:>10} size={e.size or '-'}"
                  f" [{e.update}]")


def main(argv: Optional[List[str]] = None) -> int:
    p = argparse.ArgumentParser(description="cTrader FIX QUOTE streamer")
    p.add_argument("--symbols", default="EURUSD",
                   help="comma-separated symbol names (default EURUSD)")
    p.add_argument("--duration", type=float, default=30.0,
                   help="seconds to stream (default 30)")
    p.add_argument("--csv", default=None, help="output CSV path")
    p.add_argument("--raw", default=None, help="append raw messages (redacted)")
    p.add_argument("--cred-file", default=str(DEFAULT_CRED_FILE))
    p.add_argument("--verbose", action="store_true")
    p.add_argument("--quiet", action="store_true")
    args = p.parse_args(argv)

    creds = load_credentials(Path(args.cred_file))
    print(f"[main] credentials loaded for {creds.sender_comp_id} "
          f"(QUOTE session, password never displayed)")

    client = QuoteClient(creds, verbose=args.verbose)
    client.session.connect()
    try:
        client.session.logon()
        print("[main] logon accepted")
        if SYMBOL_CACHE.exists() and (time.time() - SYMBOL_CACHE.stat().st_mtime) < 7 * 86400:
            client.symbols = {
                k: tuple(v) for k, v in json.loads(SYMBOL_CACHE.read_text()).items()
            }
            print(f"[main] symbol cache: {len(client.symbols)} instruments")
        else:
            client.fetch_symbols()
            print(f"[main] security list: {len(client.symbols)} instruments")

        wanted = [s.strip().upper() for s in args.symbols.split(",") if s.strip()]
        missing = [s for s in wanted if s not in client.symbols]
        if missing:
            print(f"[main] ERROR: symbols not offered: {missing}")
            return 2
        id_to_name = {v[0]: k for k, v in client.symbols.items()}
        ids = [client.symbols[s][0] for s in wanted]
        print(f"[main] subscribing: "
              + ", ".join(f"{s}={i}" for s, i in zip(wanted, ids)))
        client.subscribe(ids)

        rows = client.stream(
            args.duration, id_to_name,
            Path(args.csv) if args.csv else None,
            Path(args.raw) if args.raw else None,
            quiet=args.quiet,
        )
        client.session.logout()
        print(f"[main] done: {len(rows)} quote entries in {args.duration:.0f}s")
        return 0
    finally:
        client.session.close()


if __name__ == "__main__":
    sys.exit(main())
