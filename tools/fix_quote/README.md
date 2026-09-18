# fix_quote — cTrader FIX 4.4 QUOTE session client

Live market-data harness for verifying indicators against real Pepperstone
quotes. QUOTE session only (`SenderSubID=QUOTE`) — **no order routing**.

## Usage

```bash
# stream EURUSD for 60s to CSV (live account feed)
python fix_quote_client.py --symbols EURUSD --duration 60 --csv data/eurusd.csv

# multiple symbols, quiet (no per-tick printing)
python fix_quote_client.py --symbols EURUSD,GBPUSD,XAUUSD --duration 300 \
    --csv data/multi.csv --quiet

# offline tests (no network, no credentials)
python test_fix_quote_client.py
```

## Credentials

Read at runtime from `C:/Users/Ali Zoghi/AppData/Local/hermes/ctrader-API.env`
(override with `--cred-file` or `CTRADER_FIX_CRED_FILE`), key `CTRADER_FIX_PASS`.
The file is maintained **by hand by the user** — the password is never printed,
logged, or written to any output. Connection defaults (host/port/CompIDs) are
overridable via `CTRADER_FIX_HOST/PORT/USERNAME/SENDER/TARGET/SUBID` env vars.
All output paths (`--csv`, `--raw`) are guaranteed redacted (tag 554 stripped).

## Reusing from other projects (Distance, TSE, HurstRegimeAngle, ...)

```python
import sys; sys.path.insert(0, r"D:/Software/Distance/tools/fix_quote")
from fix_quote_client import load_credentials, QuoteClient

client = QuoteClient(load_credentials())
client.session.connect()
client.session.logon()
client.fetch_symbols()          # or read symbol_cache.json
client.subscribe([1])            # EURUSD = numeric id 1
rows = client.stream(30.0, {1: "EURUSD"})   # List[QuoteEntry]
```

## Wire-format evidence

Grammar extracted from help.ctrader.com/fix/specification (2026-09-02) into
`_ctrader_fix_spec.txt` / `_ctrader_fix_sendrecv.txt` in this directory.
BodyLength(9) and Checksum(10) are computed per FIX 4.4 — the doc examples'
own 9=/10= values are stale redactions and must not be copied.

Verified live 2026-09-02: Logon accepted; 1,939 instruments; EURUSD ~255
quotes/min; 63/63 snapshot pairs uncrossed; no 554 in any artifact.
