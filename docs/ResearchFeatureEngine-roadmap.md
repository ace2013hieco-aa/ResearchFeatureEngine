# Repository Optimization Roadmap (Generalized from ResearchFeatureEngine)

This is the **generalized** template derived from the Freebuff codebase evaluation
recommendations applied to `ResearchFeatureEngine`. Use it as a checklist for any
.NET + Python binding repo you want to submit to Freebuff `/earn/codebases`
(or similar platform evaluators).

> **MT4 note (repo-specific):** ❌ Skip "legacy platform, dying ecosystem, effort
> not justified." This was specific to the quant trading domain — replace with
> your own equivalent judgment call.
>
> **MT5 note (repo-specific):** ✅ Included for ResearchFeatureEngine because it
> proved the Core→Adapter boundary was truly platform-independent. For your repo,
> identify the strongest portability proof for your architecture.

**Total estimated effort:** 2–4 weeks part-time. Phases 0–2 are the critical path;
Phases 3+ amplify the signal.

---

## Phase 0 — README & Metadata Foundation
**Effort:** 4–6 hours · **Week:** 1

Make the project legible to a first-time visitor in under 30 seconds. Skip this
and nothing else lands well.

### 0.1 `LICENSE` file · 5 min
- [ ] Place an appropriate license at repo root: `LICENSE`
- **Done when:** GitHub auto-detects and shows the license in the sidebar.

### 0.2 Repo description · 10 min
- [ ] Set GitHub repo description (via repo Settings → General) to a 1-line summary
  that mentions the core value proposition, test count, license, and platform.
- **Done when:** Description visible in GitHub header.

### 0.3 README rewrite · 3–4 hours
- [ ] 2-sentence elevator pitch at the very top
- [ ] Badges block (license · version · build status · test count)
- [ ] Architecture diagram inline (see 0.4)
- [ ] Project screenshot / demo gif inline (see 0.5)
- [ ] "Quick start" section — copy-paste `pip install`/`dotnet add package` + 3-line code snippet
- [ ] "Why this exists / what problem it solves" paragraph
- **Done when:** A domain expert who knows nothing about your repo understands
  what it does and why it's interesting in 30 seconds.

### 0.4 Architecture diagram · 1–2 hours
- [ ] One visual showing the core pipeline/data flow
- [ ] Show the adapter/abstraction boundary clearly (Core vs platform-specific adapter)
- [ ] Save as `docs/architecture.png` or `.svg`; embed in README
- **Done when:** Diagram renders inline in the README preview.

### 0.5 Demo screenshot / GIF · 30 min
- [ ] Screenshot or GIF showing the project running in its target environment
- [ ] Save as `docs/screenshot-<platform>.png`; embed in README
- **Done when:** Visual proof the project actually runs.

### 0.6 Repo topics & metadata · 15 min
- [ ] Verify GitHub topics accurately reflect the project
- [ ] Add topics for each shipped adapter/platform
- **Done when:** Topics reflect each release's scope.

---

## Phase 1 — CI, Packaging, Versioning
**Effort:** 1–2 days · **Week:** 1–2

Converts "codebase" into "library." Green CI plus a real package release says
"this is shipped software."

### 1.1 GitHub Actions CI · 2–4 hours
- [ ] `.github/workflows/build-and-test.yml`
- [ ] Set up .NET (and Python if applicable) SDKs
- [ ] Steps: restore → build → test
- [ ] Trigger on `push` and `pull_request`
- [ ] Add a "build passing" badge to README
- **Done when:** Every PR shows green ✅, badge resolves.

### 1.2 Package release · 4–8 hours
- [ ] Configure packaging (`dotnet pack` / `pyproject.toml` / etc.)
- [ ] First release: **v0.1.0**
- [ ] Set up release workflow (NuGet / PyPI / npm / cargo / etc.)
- [ ] Store API key in GitHub Actions secret
- **Done when:** `dotnet add package` / `pip install` / `npm install` works
  in a clean environment.

### 1.3 `CHANGELOG.md` · 1 hour
- [ ] Initialize with `v0.1.0` entry
- [ ] Adopt [Keep a Changelog](https://keepachangelog.com) format
- **Done when:** Convention established for future releases.

### 1.4 First GitHub Release · 1 hour
- [ ] Tag `v0.1.0` + release notes (1-paragraph summary · test count · package link)
- **Done when:** Release is public and shareable.

---

## Phase 2 — Secondary Language/Adapter (Highest leverage)
**Effort:** 1–2 weeks · **Week:** 2–4

A language binding or secondary adapter doubles your addressable evaluator pool.
For quant code this is Python; for general .NET libraries this might be a JS
binding or a CLI wrapper.

### 2.1 Language binding · 3–5 days
- [ ] New project: `Adapters/YourProject.<Language>/`
- [ ] Bind the core public interface to the target language
- [ ] Accept/return native data structures (DataFrames, native arrays, etc.)
- [ ] Pin to common version range (Python 3.9–3.12, Node 18+, etc.)
- **Done when:** `import your_package; your_package.RunEngine(data)` works
  cross-platform.

### 2.2 Examples directory · 1–2 days
- [ ] `examples/01_basic_usage.<lang>` — minimal end-to-end usage
- [ ] `examples/02_full_pipeline.<lang>` — complete pipeline run
- [ ] `examples/03_<integration><.lang>` — integration with the target language's
      dominant data library (pandas, etc.)
- **Done when:** A developer can run any example without reading the source.

### 2.3 Package registry release · 1 day
- [ ] Cross-platform builds via GitHub Actions matrix
- [ ] First release: **v0.2.0** of the secondary package
- **Done when:** `pip install`/`npm install` installs cleanly on all platforms.

### 2.4 GitHub Release v0.2.0 · 1 hour
- [ ] Tag + release notes: secondary adapter live, package link, parity claim
- **Done when:** Public signal the project is now multi-language.

---

## Phase 3 — Portability Validation (Strong platform-independence proof)
**Effort:** 2–3 weeks · **Week:** 4–7

Proves the Core→Adapter abstraction is truly portable by shipping a third
platform integration. Strongest evidence of a well-factored codebase.

### 3.1 C# wrapper / adapter · 1 week
- [ ] New project: `Adapters/YourProject.<Platform>/`
- [ ] Implement the same abstraction interface for the target platform
- [ ] Golden tests against the Core
- **Done when:** A standalone test can validate correct output.

### 3.2 Platform application · 3–5 days
- [ ] Application/CLI/indicator for the target platform
- [ ] Compiles in the target's toolchain
- **Done when:** Application loads/runs in the platform environment.

### 3.3 Cross-platform parity test · 2–3 days
- [ ] Same input → output <platform-A> == output <platform-B> (within tolerance)
- [ ] Document in `docs/cross-platform-parity.md`
- **Done when:** Numerical agreement is documented and reproducible.

### 3.5 GitHub Release v0.3.0 · 1 hour
- [ ] Third-platform adapter + application release
- **Done when:** Public claim "platform-independent" now has N-platform evidence.

---

## Phase 4 — Discoverability & Traction (Amplifier)
**Effort:** 1–3 weeks · **Week:** 7+

Optional but high upside. Doesn't gate acceptance but multiplies organic discovery.

### 4.1 Technical blog post · 4–8 hours
- [ ] Topic: "Why we built our own X abstraction" or "Building a cross-platform Y pipeline"
- [ ] Publish on dev.to or personal blog
- [ ] Link from README

### 4.2 Benchmark comparison · 1–2 weeks
- [ ] Run vs 1–2 alternatives on a shared dataset
- [ ] Publish results as `docs/benchmarks.md`

### 4.3 Demo video · 2–4 hours
- [ ] 60–90 second screen recording showing the project in action
- [ ] Upload and embed in README

### 4.4 `CONTRIBUTING.md` + issue templates · 2 hours
- [ ] Build/test/PR instructions
- [ ] Label 3–5 small issues as `good first issue`

### 4.5 Documentation site (optional) · 1–2 days
- [ ] MkDocs / DocFX / etc.
- [ ] API reference, tutorials, examples

---

## Phase 5 — Submission
**Effort:** 1 hour · **When:** After Phase 2 minimum, ideally after Phase 3

### 5.1 Pre-submission checklist
- [ ] Phases 0–2 complete (foundation + secondary adapter)
- [ ] Phase 3 minimum: third-platform adapter compiles
- [ ] CI has been green for 30+ days
- [ ] README renders cleanly on desktop and mobile
- [ ] All topics, description, license, website field populated

### 5.2 Submit
- [ ] Connect repo to evaluation platform
- [ ] Confirm consent
- [ ] Wait for evaluation

---

## Summary

| Phase | Effort | Critical for acceptance? |
|---|---|---|
| 0. Foundation (README, license, diagram, screenshot) | 4–6 h | ✅ Yes |
| 1. CI + packaging + releases | 1–2 days | ✅ Yes |
| 2. Secondary adapter/language | 1–2 weeks | ✅ Highest leverage |
| 3. Portability validation | 2–3 weeks | ✅ Strong signal |
| 4. Discoverability (blog, video, benchmarks) | 1–3 weeks | ⭐ Amplifier |
| 5. Submit | 1 h | Final step |

**Critical path:** Phases 0 → 1 → 2 → 5. ~2–3 weeks gets you to submittable.

---

## Sequencing rules of thumb

1. **Never ship an adapter on an unpolished base.** Phase 0 must be done before
   Phase 2 or 3 starts.
2. **Ship v0.1.0 before v0.2.0.** A real package release makes every subsequent
   release look like progression.
3. **Pick secondary adapter by evaluator overlap.** For AI/quant repos: Python.
   For web repos: a JS binding. For CLI tools: a library wrapper.
4. **Portability proof should be the strongest signal for your stack.** For
   quant trading: cTrader ↔ MT5 parity. For web: browser ↔ Node parity.
5. **Discoverability is a flywheel, not a gate.** Pick Phase 4 items by lowest
   activation energy for you personally.
