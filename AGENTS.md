# AGENTS.md

Guidance for coding agents working in this repository.

## Project Overview

RollerGraph is a cross-platform .NET 10 desktop application built with Avalonia. It reads dyno output from serial USB at 19200 baud by default, parses CSV samples, plots HP versus speed in real time, logs raw lines, and supports CSV replay plus saved-run overlays.

The repository is intentionally split into a UI app and testable core logic:

- `src/RollerGraph.Core/` contains domain, parsing, adjustment, smoothing, scaling, logging, storage, and pipeline code. Keep this layer free of Avalonia/UI dependencies.
- `src/RollerGraph.App/` contains the Avalonia app, views, view-models, charting adapters, print/export services, and connection orchestration.
- `tests/RollerGraph.Core.Tests/` covers core behavior with xUnit and Shouldly.
- `tests/RollerGraph.App.Tests/` covers app/view-model/service behavior with test doubles.
- `samples/fixture-run.csv` is a sample replay input.

## Required Toolchain

Use the .NET 10 SDK.

Common commands:

```bash
dotnet build
dotnet test
dotnet run --project src/RollerGraph.App
```

For narrower checks:

```bash
dotnet test tests/RollerGraph.Core.Tests
dotnet test tests/RollerGraph.App.Tests
```

## Architecture Notes

### Core Pipeline

Raw serial/replay lines flow through `SamplePipeline`:

1. `CsvLineParser` parses `samplenum,speed,nm,hp,...` using invariant culture.
2. `SampleAdjuster` applies per-channel settings for Speed, NM, and HP.
3. The negative-value filter drops post-adjustment samples where Speed, NM, or HP is below zero.
4. The min-speed filter drops samples below `Settings.MinSpeedKmh`.
5. Optional peak-preserving smoothing runs over accepted samples.

Filtered samples are not plotted, but raw lines are still logged by the connection layer. Preserve this distinction.

### App Coordination

`MainWindowViewModel` is the top-level coordinator. It should stay relatively thin:

- `ConnectionController` owns live serial/replay lifecycle, logging session boundaries, and pipeline execution.
- `ChartViewModel` owns chart observable state.
- `SavedRunsViewModel` owns saved-run CRUD and overlay state.
- View services such as file picking, settings dialogs, exporting, and printing are injected through small interfaces under `src/RollerGraph.App/Services/`.

When adding UI behavior, prefer a small interface/test double over direct platform or Avalonia APIs in view-model logic.

### Serial and Replay

Serial abstractions live under `src/RollerGraph.Core/Serial/`.

- `ISerialSource` emits raw lines and errors.
- `SystemSerialSourceFactory` is the concrete live/replay factory.
- `ReplaySerialSource` should remain suitable for deterministic tests and fixture-based workflows.

Avoid adding serial-port or filesystem assumptions directly to view-models.

### Storage

Application data paths are centralized in `AppDataPaths`.

- Logs are session CSV files under the OS-specific app data `logs/` directory.
- Saved runs are CSV files with `#` metadata headers under `runs/`.
- Settings are persisted as JSON.

Keep file formats backward-compatible unless the task explicitly calls for a migration.

## Coding Conventions

- Nullable reference types and implicit usings are enabled.
- Prefer small, focused classes with explicit interfaces at IO/UI boundaries.
- Use invariant culture for wire formats, saved CSV, and numeric parsing/formatting.
- Use records/record structs for simple immutable data where that matches existing style.
- Keep `RollerGraph.Core` independent from Avalonia, LiveCharts, and other UI packages.
- Do not hand-roll platform behavior in shared view-models; put it behind a service abstraction.
- Keep comments sparse but useful. Existing comments usually explain lifecycle, threading, or domain intent.
- This repo currently uses ASCII text in source/docs; keep new content ASCII unless there is a clear reason.

## Clean Code and SOLID

Adhere to clean code and SOLID principles when changing or adding code:

- Keep classes and methods focused on one clear responsibility. If a method starts mixing parsing, IO, UI state, and formatting, split the work behind existing boundaries.
- Prefer readable names and straightforward control flow over cleverness. Code should communicate intent without relying on long comments.
- Depend on abstractions at IO, platform, serial, storage, and UI boundaries. View-models should consume small interfaces and test doubles rather than concrete framework APIs.
- Keep modules open for extension but avoid broad rewrites. Add focused collaborators when behavior varies, and preserve stable public contracts unless the task requires a contract change.
- Respect interface segregation. Do not grow large service interfaces for one-off needs; introduce narrow interfaces that describe the consumer's actual dependency.
- Preserve substitutability in implementations. Test doubles, replay sources, and concrete serial/storage services should honor the same behavioral expectations.
- Keep dependency direction clean: `RollerGraph.Core` must not depend on app/UI packages, and app services should adapt platform details at the edge.
- Avoid unnecessary abstraction. Introduce a new layer only when it reduces real duplication, isolates volatility, or makes behavior easier to test.

## UI Guidance

The app is an operational desktop tool, not a marketing surface. Favor dense, predictable controls and readable status over decorative layouts.

Existing UI patterns:

- Avalonia XAML views under `src/RollerGraph.App/Views/`.
- CommunityToolkit.Mvvm attributes such as `[ObservableProperty]` and `[RelayCommand]`.
- LiveCharts adapters under `src/RollerGraph.App/Charting/`.
- View-model commands bridge to view services for dialogs, export, print, and file picking.

When changing XAML, verify bindings against the corresponding view-model. Compiled bindings are enabled by default.

## Testing Expectations

Add or update tests for behavior changes. Keep tests close to the layer being changed:

- Parser, pipeline, adjustment, smoothing, scaling, logging, and storage changes belong in `RollerGraph.Core.Tests`.
- View-model, command, print/export, and connection orchestration changes belong in `RollerGraph.App.Tests`.

Use xUnit facts and Shouldly assertions, matching the current test style.

Before handing off a code change, run at least the relevant test project. Prefer `dotnet test` for cross-cutting changes.

## Domain Rules To Preserve

- CSV input requires only the first four fields: sample number, speed, NM, HP. Extra trailing fields are allowed.
- Numeric parsing uses `.` decimal and invariant culture.
- The parser does no unit conversion. Scaling and calibration belong in settings adjustments.
- NM is captured, tracked, saved, and overlaid, but it is not plotted on the live chart as a primary live series.
- Bad lines are counted; min-speed and negative-value filtered samples are silently dropped.
- Zero values are valid. Only values below zero are rejected by the negative-value filter.
- Smoothing affects the plotted live/replay curve only. Peak HP/NM/speed stats and saved current-run samples should use adjusted, unsmoothed measurement data. When the live HP curve is smoothed, keep the raw HP peak visible with the chart's peak marker.
- Reset clears the chart/live samples and, when connected, restarts the active live/replay source so the next run gets a fresh acquisition boundary and log session.
- When accepted speed drops during a run, treat the run as stopped: close the active log, suppress coast-down samples, and start a fresh chart/log session when speed rises again.
- Printed/exported PNG snapshots should include peak HP, peak NM, and peak speed stats alongside the chart.
- Settings changes should preserve the selected/last port where applicable.

## Git and Generated Files

- Do not revert unrelated user changes.
- Do not commit build outputs from `bin/` or `obj/`.
- Avoid touching solution/project metadata unless adding/removing projects or packages.
- If package versions need to change, make the smallest compatible update and verify with tests.
