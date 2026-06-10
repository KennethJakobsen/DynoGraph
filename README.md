# RollerGraph

Cross-platform .NET 10 desktop app that reads dyno output from a serial USB
connection (19200 BAUD) and plots HP and NM versus speed in real time, with
auto-scaling axes and CSV logging/replay.

This is **Phase 1 + 2 + 3** of a phased delivery. All originally planned
features plus the Phase 3 additions (saved runs, keyboard shortcuts, print)
are implemented.

---

## Features

### Phase 1 (foundational)

- Connect to any USB serial port at 19200 BAUD (configurable in Settings)
- Live dual-Y-axis chart: HP on the left, NM on the right, speed on the X axis
- Grow-only "nice number" axis scaling (10, 25, 50, 100, 250, 500...)
- Manual **Reset** button clears the chart and opens a new log file
- **Replay** any previously logged (or hand-crafted) CSV file
- All raw lines are logged to a timestamped CSV file under the OS-appropriate
  user data folder
- Malformed lines are silently dropped and counted in the status bar
- Follows the OS dark/light theme

### Phase 2 (polish & insights)

- **Settings dialog** for baud rate, minimum speed, smoothing window, and
  default axis maxima. Settings persist between launches.
- **Per-channel adjustments** for Speed, NM, and HP. Each channel supports
  either a simple linear correction (`value' = value * Factor + Offset`) or
  an arbitrary math expression with `x` bound to the input value. Useful when
  the dyno reads, say, 92% of true HP - set the HP expression to `x / 0.92`.
- **Smoothing toggle** in the toolbar applies a rolling-average over HP, NM,
  and speed. Window size is configured in Settings (default 5).
- **Peak stats panel** on the right side shows live peak HP, peak NM (with
  the speed each occurred at), and peak speed, in big readable numerals.
- **Export PNG** button saves the current chart at its on-screen resolution.
- **Status bar** shows sample count, bad-line count, current log file, and
  connection status.
- **Last-used port** is remembered between launches.

#### Expression syntax (Adjustments tab)

| Category | Supported |
|---|---|
| Variables | `x` (input value) |
| Constants | `pi`, `e` |
| Operators | `+`, `-`, `*`, `/`, `^` (power, right-associative), unary `-` |
| Functions | `abs`, `sqrt`, `log`, `log10`, `exp`, `sin`, `cos`, `tan`, `min(a,b)`, `max(a,b)`, `pow(a,b)` |
| Numbers | `1.5`, `0.92`, `1e3`, `2.5E-2` (always `.` decimal) |

Examples:

- `x * 1.05` - scale by 5%
- `x / 0.92` - correct for 92% drivetrain efficiency
- `x * 1.85` - convert kW to HP
- `pow(x, 0.98)` - mild non-linear correction
- `max(x - 2, 0)` - subtract a deadband, clip negatives

### Phase 3 (saved runs, shortcuts, print)

- **Saved Runs**: capture any chart's data as a named run that's automatically
  written to `{LocalAppData}/RollerGraph/runs/<slug>.csv`. Multiple saved runs
  can be overlaid on the chart simultaneously for tuning comparisons
  (e.g. "86 nozzle" vs "102 nozzle").
- **Save Run** (Cmd/Ctrl+B) prompts for a name and captures the current chart.
- **Load Run...** opens a CSV file as a new saved run. Both saved-run files
  and regular session logs are accepted.
- **Saved Runs panel** lists every saved run with a color swatch, sample count,
  visibility checkbox, rename and delete buttons. Visibility is persisted.
- **Auto-load on launch**: every saved run is restored to the chart at startup.
- **Clear All Saved Runs** (Cmd/Ctrl+Shift+B) removes all saved runs at once.
- **Print** (Cmd/Ctrl+P or Enter) renders the chart to a PNG and hands it off
  to the OS print pipeline (Preview on macOS, default viewer on Linux,
  shell print on Windows).

### Keyboard shortcuts

| Shortcut | Action |
|---|---|
| Esc | Reset |
| Cmd / Ctrl + R | Reset |
| Cmd / Ctrl + K | Toggle Connect / Disconnect |
| Cmd / Ctrl + O | Replay CSV... |
| Cmd / Ctrl + E | Export PNG... |
| Cmd / Ctrl + P | Print |
| Enter | Print (when no text input has focus) |
| Cmd / Ctrl + , | Open Settings |
| Cmd / Ctrl + B | Save Current Run |
| Cmd / Ctrl + Shift + B | Clear All Saved Runs |
| Space | Toggle Smoothing (when no text input has focus) |

## CSV input format

Each line, newline-delimited (`\n` or `\r\n`):

```
samplenum,speed,nm,hp_x10,NA,NA,NA,NA,NA
```

| Field | Type | Meaning |
|---|---|---|
| `samplenum` | int | Sample counter, incrementing from dyno power-on |
| `speed` | double | Speed in km/h |
| `nm` | double | Torque in newton-meters (raw) |
| `hp_x10` | double | Horsepower * 10 - app divides by 10 on read |
| 5..9 | any | Currently unused (`NA` placeholders are fine) |

Only the first four fields are required; trailing fields may be present or
absent. Bad lines are dropped silently and counted.

## Log file location

| OS | Path |
|---|---|
| macOS | `~/Library/Application Support/RollerGraph/logs/` |
| Linux | `~/.local/share/RollerGraph/logs/` |
| Windows | `%LOCALAPPDATA%\RollerGraph\logs\` |

Each session creates a file named `session-YYYYMMDD-HHMMSS.csv`. A new file
is opened on every Connect and every Reset.

## Saved runs location

| OS | Path |
|---|---|
| macOS | `~/Library/Application Support/RollerGraph/runs/` |
| Linux | `~/.local/share/RollerGraph/runs/` |
| Windows | `%LOCALAPPDATA%\RollerGraph\runs\` |

One CSV per saved run, named after the slugified run name. The first few
lines are `#`-prefixed metadata (display name, color, visibility, created
timestamp), followed by a header and the captured samples.

## Settings file location

| OS | Path |
|---|---|
| macOS | `~/Library/Application Support/RollerGraph/settings.json` |
| Linux | `~/.local/share/RollerGraph/settings.json` |
| Windows | `%LOCALAPPDATA%\RollerGraph\settings.json` |

The file is written atomically; deleting it restores defaults on next launch.

---

## Build & run

Requires the **.NET 10 SDK**.

```bash
# Build the whole solution
dotnet build

# Run the desktop app
dotnet run --project src/RollerGraph.App

# Run the unit tests
dotnet test
```

## Publishing self-contained binaries

```bash
# macOS (Apple Silicon)
dotnet publish src/RollerGraph.App -c Release -r osx-arm64 --self-contained true

# macOS (Intel)
dotnet publish src/RollerGraph.App -c Release -r osx-x64 --self-contained true

# Linux x64
dotnet publish src/RollerGraph.App -c Release -r linux-x64 --self-contained true

# Windows x64
dotnet publish src/RollerGraph.App -c Release -r win-x64 --self-contained true
```

The published output ends up under
`src/RollerGraph.App/bin/Release/net10.0/<rid>/publish/`.

---

## Continuous integration & releases

GitHub Actions workflows live under `.github/workflows/`.

### CI (`ci.yml`)

Runs on every push to `main` and every pull request:

1. Restores, builds, and tests on **Linux**, **macOS**, and **Windows** in parallel.
2. Publishes test results to the PR check summary via `dorny/test-reporter`.
3. Uploads `*.trx` files as workflow artifacts (14-day retention).

A red test fails the build, blocking the PR.

### Releases (`release.yml`)

Triggered by pushing a version tag of the form `vX.Y.Z` (or `vX.Y.Z-rc.N`
for a pre-release):

```bash
git tag v1.0.0
git push origin v1.0.0
```

Per tag, the workflow:

1. Re-runs all tests on Linux/macOS/Windows.
2. Publishes a self-contained, single-file binary for **four** runtime
   identifiers in parallel: `osx-arm64`, `osx-x64`, `linux-x64`, `win-x64`.
   Native libraries (SkiaSharp, HarfBuzz, AvaloniaNative) are extracted
   into the executable via `IncludeNativeLibrariesForSelfExtract`.
3. Compresses each output (`.zip` for macOS/Windows, `.tar.gz` for Linux)
   and emits a SHA-256 checksum next to it.
4. Uploads each RID's archive + checksum as a workflow artifact
   (30-day retention).
5. Creates a GitHub Release tagged with the version, attaches every
   archive and checksum, and auto-generates release notes from the
   commits since the previous tag. Tags containing `-` (e.g. `v1.0.0-rc.1`)
   are marked as pre-releases.

Tested locally: a publish for `osx-arm64` produces a single 45 MB
executable with no satellite native libraries.

### Manual / dry-run releases

The release workflow also accepts a `workflow_dispatch` trigger from the
Actions UI, which builds all four archives but skips the GitHub Release
step. Useful for verifying packaging changes without cutting a real version.

---

## Per-OS notes

### macOS
USB-serial devices typically appear as `/dev/cu.usbserial-*` or
`/dev/cu.usbmodem-*`. If the dropdown is empty, your USB-serial chip likely
needs its vendor driver installed (e.g. FTDI, CP210x, CH340).

### Linux
Devices appear as `/dev/ttyUSB*` (FTDI/CP210x) or `/dev/ttyACM*` (modems / CDC).
Your user must be in the `dialout` group:

```bash
sudo usermod -aG dialout $USER
# log out and back in for the group change to take effect
```

### Windows
Devices appear as `COM3`, `COM4`, etc. No special permissions needed.

---

## Trying replay without hardware

A small fixture CSV ships in this repo at
[`samples/fixture-run.csv`](samples/fixture-run.csv). Open the app, click
**Replay CSV...**, and pick that file - you'll see HP and NM rise with speed
and then taper off.

---

## Project layout

```
RollerGraph.slnx
├── src/
│   ├── RollerGraph.Core/          # No UI deps - pure, testable
│   │   ├── Adjustments/           # ChannelAdjustment, SampleAdjuster, ExpressionCompiler
│   │   ├── Logging/               # CsvSessionLogger
│   │   ├── Models/                # Sample, SavedRun, Settings, SettingsStore
│   │   ├── Parsing/               # CsvLineParser
│   │   ├── Pipeline/              # SamplePipeline (parse + adjust + filter + smooth)
│   │   ├── Scaling/               # NiceNumber
│   │   ├── Serial/                # ISerialSource, RjcpSerialSource, ReplaySerialSource, LineBuffer
│   │   ├── Smoothing/             # RollingAverage, SampleSmoother
│   │   └── Storage/               # AppDataPaths, FileSavedRunStore, RunColorPalette, RunSlugger
│   └── RollerGraph.App/           # Avalonia 12 app
│       ├── Charting/              # LiveChartsChartRenderer + snapshotter
│       ├── Connection/            # ConnectionController (lifecycle of one serial / replay session)
│       ├── Printing/              # IPrintLauncher + PlatformPrintLauncher
│       ├── Services/              # IUiDispatcher, ICsvFilePicker, IChartPrinter, ...
│       ├── ViewModels/            # MainWindowViewModel, ChartViewModel, SavedRunsViewModel,
│       │                          # SettingsViewModel, SavedRunViewModel, converters
│       └── Views/                 # MainWindow, SettingsWindow, RunNameDialog, ConfirmDialog
└── tests/
    ├── RollerGraph.Core.Tests/    # xUnit + Shouldly - Core library coverage
    └── RollerGraph.App.Tests/     # xUnit + Shouldly - VM / coordinator coverage
```

## Tech stack

| Component | Library | License |
|---|---|---|
| Framework | .NET 10 | MIT |
| UI | Avalonia 12 | MIT |
| MVVM | CommunityToolkit.Mvvm | MIT |
| Charts | LiveChartsCore.SkiaSharpView.Avalonia | MIT |
| Serial | RJCP SerialPortStream | MS-PL |
| Tests | xUnit + Shouldly | Apache 2.0 / BSD |

All dependencies are free and open-source.
