# Distribution Shape Statistics (Skewness & Kurtosis)

The Statistics stage computes two distribution-shape statistics through
the same model extension point as every other statistic
(`IStatisticModel` → `StatisticType` → `StatisticsEngine` →
`StatisticsRuntimeValues` → publisher/output).

## Estimators

| Statistic | Estimator | Minimum n |
| --- | --- | --- |
| `StatisticType.Skewness` | Fisher–Pearson bias-corrected sample skewness `G1 = sqrt(n(n-1))/(n-2) · m3/m2^(3/2)` | 3 |
| `StatisticType.Kurtosis` | Fisher bias-corrected **excess** kurtosis `G2 = ((n+1)(m4/m2² − 3) + 6)(n−1) / ((n−2)(n−3))` | 4 |

where `m_k = (1/n) · Σ(x_i − mean)^k` are central moments. Both use a
**two-pass central-moment calculation** (pass 1: mean; pass 2:
deviations from the mean) — never naive one-pass power sums — so the
result stays accurate when the mean is much larger than the dispersion
(e.g. raw prices around 1.0e6 with O(1) moves).

Kurtosis is **excess** kurtosis: normal-like data gives `G2 ≈ 0`, not
the raw convention `b2 ≈ 3`.

## Zero variance

If `m2 == 0` (flat window), both statistics are mathematically
undefined. The models return NaN, which the engine treats as
"do not publish" — the runtime retains its **last published value**,
exactly as when the observation count is below the model's minimum.
Nothing is published (no `0`, no NaN) and nothing is thrown.

## Source semantics

The models are **source-blind**: they receive plain numeric
observations and never know whether those came from `Close`,
`SimpleReturn`, or `LogReturn` — the source is chosen by the caller
(`EngineOptions.StatisticsSource` / `StatisticsSource`), and the
default (`Close`) is unchanged.

Skewness/kurtosis are intended to be interpreted as **return-
distribution shape statistics**, and `LogReturn` is the canonical
research input.

## Reliability guidance

- Exploratory use: `n >= 50`
- Inference-grade use: `n >= 100`

## Interpretation caveats

Kurtosis is **highly sensitive to extreme observations**: a rolling
kurtosis spike can represent either a genuine tail event **or** a
recent regime break / mixed-volatility window — treat it as a flag to
inspect the underlying window, not as a standalone verdict.
