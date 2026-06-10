# RollerGraph

Cross-platform .NET 10 desktop app that reads dyno output from a serial USB
connection (19200 BAUD) and plots HP and NM versus speed in real time, with
auto-scaling axes and CSV logging/replay.

This is **Phase 1 + 2** of a phased delivery. All originally planned features
are now implemented.

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
RollerGraph.sln
├── src/
│   ├── RollerGraph.Core/          # No UI deps - pure, testable
│   │   ├── Models/                # Sample, Settings, SettingsStore
│   │   ├── Parsing/               # CsvLineParser
│   │   ├── Serial/                # ISerialSource, RjcpSerialSource, ReplaySerialSource, LineBuffer
│   │   ├── Scaling/               # NiceNumber
│   │   ├── Smoothing/             # RollingAverage, SampleSmoother
│   │   ├── Adjustments/           # ChannelAdjustment, SampleAdjuster, ExpressionParser
│   │   └── Logging/               # CsvSessionLogger
│   └── RollerGraph.App/           # Avalonia 12 app
│       ├── ViewModels/            # MainWindowViewModel, ChartViewModel, SettingsViewModel,
│       │                          # ChannelAdjustmentViewModel, converters
│       ├── Views/                 # MainWindow.axaml, SettingsWindow.axaml
│       └── Services/              # IUiDispatcher
└── tests/
    └── RollerGraph.Core.Tests/    # xUnit + Shouldly
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
