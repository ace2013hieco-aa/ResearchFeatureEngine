"""Offline tests for fix_quote_client — no network, no live credentials.

Run: python -m pytest test_fix_quote_client.py -q   (or: python test_fix_quote_client.py)
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from fix_quote_client import (  # noqa: E402
    BEGIN_STRING,
    FixCredentials,
    build_message,
    parse_message,
    parse_md_snapshot,
    parse_md_incremental,
    parse_security_list,
    redact_message,
    SOH,
    SOH_B,
)


def make_creds() -> FixCredentials:
    return FixCredentials(
        host="example.invalid", port=5211, username="1160380",
        password="SuperSecret123!", sender_comp_id="live.pepperstone.1160380",
        target_comp_id="cServer", sender_sub_id="QUOTE",
    )


# ── message construction ──────────────────────────────────────────────
def test_build_message_frame_format():
    creds = make_creds()
    raw = build_message(creds, 1, "A", [(98, "0"), (108, "30"), (141, "Y"),
                                        (553, "1160380"), (554, "pw")])
    fields = [f.split("=", 1) for f in raw.rstrip(SOH).split(SOH)]
    tags = {int(t): v for t, v in fields}
    assert tags[8] == BEGIN_STRING
    assert tags[35] == "A"
    assert tags[49] == "live.pepperstone.1160380"
    assert tags[56] == "cServer"
    assert tags[50] == tags[57] == "QUOTE"
    assert tags[34] == "1"
    assert tags[141] == "Y"
    # checksum position: trailer is the last field
    assert fields[-1][0] == "10"
    assert len(fields[-1][1]) == 3


def test_body_length_is_exact():
    creds = make_creds()
    raw = build_message(creds, 2, "V", [(262, "req1"), (263, "1"), (264, "1"),
                                        (265, "1"), (146, "1"), (55, "1"),
                                        (267, "2"), (269, "0"), (269, "1")])
    # body = everything after 9=<len> up to (excluding) 10=
    prefix, rest = raw.split(f"9=", 1)
    body_len_field, body_and_trailer = rest.split(SOH, 1)
    body = body_and_trailer[: -len("10=xxx" + SOH)]
    assert int(body_len_field) == len(body.encode("ascii"))


def test_checksum_is_correct():
    creds = make_creds()
    raw = build_message(creds, 3, "0", [(112, "abc")])
    # checksum = sum of all bytes before 10= field, mod 256
    before_checksum = raw[: raw.rfind("10=")].encode("ascii")
    got = int(raw[raw.rfind("10=") + 3 :].rstrip(SOH))
    assert got == sum(before_checksum) % 256
    assert 0 <= got <= 255


def test_seq_num_increments_via_9_length_consistency():
    # two messages, different seq nums -> different but valid lengths
    creds = make_creds()
    r1 = build_message(creds, 10, "0", [])
    r2 = build_message(creds, 11, "0", [])
    s1 = parse_message(r1)
    s2 = parse_message(r2)
    assert s1.seq == 10 and s2.seq == 11
    for r in (r1, r2):
        before = r[: r.rfind("10=")].encode("ascii")
        got = int(r[r.rfind("10=") + 3 :].rstrip(SOH))
        assert got == sum(before) % 256


# ── parsing ──────────────────────────────────────────────────────────
def test_parse_message_roundtrip():
    creds = make_creds()
    raw = build_message(creds, 1, "A", [(98, "0"), (553, "1160380"), (554, "pw")])
    m = parse_message(raw)
    assert m.msg_type == "A"
    assert m.seq == 1
    assert m.get(553) == "1160380"
    assert m.get(554) == "pw"
    assert m.get(49) == "live.pepperstone.1160380"


def test_parse_repeating_groups_keep_order():
    raw = (f"8={BEGIN_STRING}{SOH}9=99{SOH}35=W{SOH}34=2{SOH}55=1{SOH}"
           f"268=2{SOH}269=0{SOH}270=1.06625{SOH}271=1000000{SOH}"
           f"269=1{SOH}270=1.0663{SOH}10=000{SOH}")
    m = parse_message(raw)
    sym, entries = parse_md_snapshot(m)
    assert sym == 1
    assert len(entries) == 2
    assert entries[0].side == "bid" and entries[0].price == "1.06625"
    assert entries[1].side == "ask" and entries[1].price == "1.0663"


def test_parse_incremental_groups():
    raw = (f"8={BEGIN_STRING}{SOH}9=99{SOH}35=X{SOH}34=3{SOH}"
           f"268=2{SOH}279=0{SOH}269=1{SOH}278=16{SOH}55=1{SOH}270=1.11134{SOH}271=5000000{SOH}"
           f"279=0{SOH}269=0{SOH}278=17{SOH}55=1{SOH}270=1.11132{SOH}10=000{SOH}")
    m = parse_message(raw)
    entries = parse_md_incremental(m)
    assert len(entries) == 2
    assert entries[0].update == "new" and entries[0].side == "ask"
    assert entries[0].price == "1.11134" and entries[0].size == "5000000"
    assert entries[1].side == "bid" and entries[1].price == "1.11132"
    assert entries[1].symbol_id == 1


def test_parse_security_list_mapping():
    raw = (f"8={BEGIN_STRING}{SOH}9=99{SOH}35=y{SOH}34=4{SOH}146=3{SOH}"
           f"55=1{SOH}1007=EURUSD{SOH}1008=5{SOH}"
           f"55=2{SOH}1007=GBPUSD{SOH}1008=5{SOH}"
           f"55=3{SOH}1007=EURJPY{SOH}1008=3{SOH}10=000{SOH}")
    m = parse_message(raw)
    syms = parse_security_list(m)
    assert syms["EURUSD"] == (1, 5)
    assert syms["GBPUSD"] == (2, 5)
    assert syms["EURJPY"] == (3, 3)


# ── secret hygiene ────────────────────────────────────────────────────
def test_repr_never_leaks_password():
    creds = make_creds()
    r = repr(creds)
    assert "SuperSecret123!" not in r
    assert "<redacted>" in r


def test_redact_message_strips_554():
    creds = make_creds()
    raw = build_message(creds, 1, "A", [(553, "1160380"), (554, "SuperSecret123!")])
    red = redact_message(raw)
    assert "SuperSecret123!" not in red
    assert "554=<redacted>" in red


def test_logged_output_shape():
    # verbose mode prints redact_message(...); simulate the f-string path
    creds = make_creds()
    raw = build_message(creds, 1, "A", [(554, "pw")])
    printed = f">>> {redact_message(raw.rstrip(SOH)).replace(SOH, '|')}"
    assert "554=pw" not in printed
    assert "554=<redacted>" in printed


if __name__ == "main_tests":
    pass

if __name__ == "__main__":
    # tiny runner (no pytest dependency)
    import traceback
    fns = [v for k, v in sorted(globals().items()) if k.startswith("test_") and callable(v)]
    failed = 0
    for fn in fns:
        try:
            fn()
            print(f"PASS {fn.__name__}")
        except Exception:
            failed += 1
            print(f"FAIL {fn.__name__}")
            traceback.print_exc()
    print(f"{len(fns) - failed}/{len(fns)} passed")
    sys.exit(1 if failed else 0)
