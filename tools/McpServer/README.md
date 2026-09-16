# ResearchFeatureEngine MCP Server (C#)

Stdio MCP server that exposes the **production** engine — no math duplication.
Every `compute_features` value is read from `EngineValues` after `engine.Update()`,
on a pipeline composed by `ResearchFeatureEngineBuilder` with the ratified
defaults (ATRSmooth 16/5.1/100, scale 14, stats window 252, Close, TrailingStopPosition).

## Tools

| Tool | Args | Returns |
|---|---|---|
| `describe_engine` | – | pipeline order, reference models, CSV contract |
| `list_stages` | `mode` | stages registered for `atrsmooth2\|darvasbox\|hma\|hmaatrsmooth` |
| `validate_data` | `csvPath` | `{ok, rows, first, last, issues}` — fail-closed |
| `compute_features` | `csvPath, mode, maxBars(1-500)` | last N bars: `time, close, reference, regime, distance, scale, normalized, reversal, mean, std` |

CSV: `fixture_v1` (`DateTime,Open,High,Low,Close,Volume`); also accepts
`recorder_v1_1` (`OpenTimeUtc,...,Spread`) by ignoring Spread. Max 20k rows/run.

## Build & run

```powershell
dotnet build tools/McpServer/ResearchFeatureEngine.McpServer.csproj
dotnet run --project tools/McpServer/ResearchFeatureEngine.McpServer.csproj
```

## Client config (example — Claude Desktop / opencode)

```json
{
  "mcpServers": {
    "research-feature-engine": {
      "command": "dotnet",
      "args": ["run", "--project", "<repo-path>/tools/McpServer/ResearchFeatureEngine.McpServer.csproj", "--no-launch-profile"],
      "cwd": "<repo-path>"
    }
  }
}
```

For evaluators: point `csvPath` at `Tests/TestData/EURUSD_M1_10000.csv`
(fixture_v1) and call `compute_features` with each of the 4 modes.
