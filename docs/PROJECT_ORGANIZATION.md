# Project Organization

## Overview

This document defines the current architecture of **ArbSh** as an Arabic-first shell and terminal platform.

**Current Version:** 0.9.0
**Status:** Phase 6 In Progress - Windows/Linux Process Ownership Implemented
**Next Step:** Linux CI Receipt, Live Streams, and PTY/ConPTY

ArbSh now uses a host-agnostic core engine with separate presentation hosts:
- `ArbSh.Core` contains parsing, execution, cmdlets, BiDi/i18n logic, and the
  non-interactive structured process boundary.
- `ArbSh.Console` is the legacy/compatibility console host.
- `ArbSh.Terminal` is the new Avalonia GUI terminal host.
- `ArbSh.ProcessFixture` is a test-only external executable for process-contract
  verification.

## Current Directory Structure

```text
ArbSh/
├── docs/
│   ├── CHANGELOG.md
│   ├── PROJECT_ORGANIZATION.md
│   ├── USAGE_EXAMPLES.md
│   ├── BIDI_P_RULES_DESIGN.md
│   ├── BIDI_X_RULES_DESIGN.md
│   ├── BIDI_W_RULES_DESIGN.md
│   ├── BIDI_N_RULES_DESIGN.md
│   ├── BIDI_I_RULES_DESIGN.md
│   └── BIDI_L_RULES_DESIGN.md
├── src_csharp/
│   ├── ArbSh.sln
│   ├── ArbSh.Core/
│   │   ├── Commands/
│   │   ├── Hosting/
│   │   ├── I18n/
│   │   ├── Models/
│   │   ├── Parsing/
│   │   ├── Processes/
│   │   ├── Executor.cs
│   │   ├── Parser.cs
│   │   └── ShellEngine.cs
│   ├── ArbSh.Console/
│   │   ├── I18n/
│   │   ├── ConsoleExecutionSink.cs
│   │   └── Program.cs
│   ├── ArbSh.Terminal/
│   │   ├── Assets/
│   │   │   └── Fonts/
│   │   ├── Input/
│   │   │   ├── TerminalInputBuffer.cs
│   │   │   ├── OutputSelectionBuffer.cs
│   │   │   └── SelectionRange.cs
│   │   ├── Rendering/
│   │   │   ├── AnsiSgrParser.cs
│   │   │   ├── AnsiColorSpec.cs
│   │   │   ├── AnsiStyleModel.cs
│   │   │   ├── AnsiPalette.cs
│   │   │   ├── TerminalTheme.cs
│   │   │   ├── TerminalSurface.cs
│   │   │   ├── TerminalTextPipeline.cs
│   │   │   ├── TerminalLayoutEngine.cs
│   │   │   ├── TerminalFrameLayout.cs
│   │   │   ├── PromptLayoutSnapshot.cs
│   │   │   └── TerminalRenderConfig.cs
│   │   ├── ViewModels/
│   │   ├── Models/
│   │   ├── App.axaml
│   │   ├── MainWindow.axaml
│   │   └── Program.cs
│   ├── ArbSh.ProcessFixture/
│   │   └── Program.cs
│   └── ArbSh.Test/
│       ├── BidiAlgorithmTests.cs
│       ├── BidiTestConformanceTests.cs
│       ├── ArabicShapingTests.cs
│       ├── TerminalTextPipelineTests.cs
│       ├── TerminalLayoutEngineTests.cs
│       ├── LogicalVisualSeparationTests.cs
│       ├── TerminalInputBufferTests.cs
│       ├── TerminalShellChromeTests.cs
│       ├── AnsiSgrParserTests.cs
│       ├── ExternalProcessRunnerTests.cs
│       └── AnsiPaletteTests.cs
├── ROADMAP.md
└── System.md
```

## Architecture Layers

| Layer | Project | Responsibility |
|---|---|---|
| Core Engine | `src_csharp/ArbSh.Core` | Parsing, tokenization, parameter binding, cmdlet execution, pipeline, BiDi logic. |
| Host Abstraction | `src_csharp/ArbSh.Core/Hosting` | `IExecutionSink` and sink-aware output boundary between logic and rendering. |
| Structured Processes | `src_csharp/ArbSh.Core/Processes` | Direct argv/cwd/environment launch, UTF-8 stream capture, exit/failure classification, cancellation, and explicit process-tree ownership. |
| Console Host | `src_csharp/ArbSh.Console` | CLI host loop, console-specific I/O, backward-compatible execution sink. |
| GUI Terminal Host | `src_csharp/ArbSh.Terminal` | Avalonia app, view models, rendering surface, RTL-first terminal UX. |
| Process Fixture | `src_csharp/ArbSh.ProcessFixture` | Test-only executable for deterministic process-boundary checks. |
| Test Suite | `src_csharp/ArbSh.Test` | Unit and conformance tests for BiDi and core behavior. |

## Key Design Principles

1. **Logical vs Visual Split**: Logical command text and pipeline state remain in `ArbSh.Core`. Visual ordering/shaping belongs to host/rendering layers only.
2. **Arabic-First UX**: Arabic command aliases and UAX #9 compliance are first-class requirements.
3. **Host Independence**: Core execution must not directly write to `System.Console`; all output routes through execution sinks.
4. **Concurrency Safety**: Pipeline stages use task-based execution and `BlockingCollection<T>` patterns.
5. **MVVM for GUI**: Avalonia host follows view/viewmodel separation for terminal state and rendering.

## Build and Run

```powershell
# Build full solution
dotnet build src_csharp/ArbSh.sln

# Run console host
dotnet run --project src_csharp/ArbSh.Console

# Run Avalonia terminal host
dotnet run --project src_csharp/ArbSh.Terminal

# Run tests
dotnet test src_csharp/ArbSh.Test
```

## Current Phase Snapshot

### Completed in Phase 5.1
- Extracted shared engine into `ArbSh.Core`.
- Moved cmdlets/models/parser/executor/BiDi code into the core project.
- Added sink-based host boundary (`IExecutionSink`, `CoreConsole`, `ShellEngine`).
- Added Avalonia terminal bootstrap project (`ArbSh.Terminal`).
- Added packaged font assets for terminal rendering and configured asset-first font fallback.

### Completed in Phase 5.2 (Output + Prompt Rendering)
- Added a terminal rendering pipeline that converts logical text to visual display text at the UI boundary only.
- Implemented `TerminalTextPipeline` and `TerminalLayoutEngine` for line shaping/reordering and frame composition.
- Refactored `TerminalSurface` to consume draw instructions instead of performing ad-hoc reordering logic inline.
- Added runtime Arabic/Latin font fallback configuration for terminal text rendering.
- Added ANSI SGR parsing and span-based style metadata generation (16/256/truecolor).
- Added ArbSh navy theme + ANSI palette mapping in terminal rendering config.
- Updated output rendering to apply ANSI foreground/background/styles without mutating logical text.

### Completed in Phase 5.3 (RTL Input + Cursor + Selection)
- Added a dedicated input subsystem for logical text, caret, and selection state (`TerminalInputBuffer`).
- Implemented visual caret movement and hit-testing for mixed Arabic/Latin prompt input using Avalonia text layout APIs.
- Added pointer-based caret placement and drag selection on the prompt line.
- Added clipboard copy/cut/paste support for selected input text.

### Completed in Phase 5 Closure
- Closed typography and color/theming remaining tasks from Phase 5.1 and 5.2.
- Kept prompt/caret behavior untouched while extending output styling path.

### Completed in Phase 5.4 (Terminal Emulator Behaviors)
- Added scrollback offset virtualization with mouse wheel and PageUp/PageDown navigation.
- Kept prompt pinned to bottom while output viewport moves through history.
- Added output-line selection and logical-order clipboard copy (`Ctrl+C`) for scrollback content.

### Completed in Phase 6 (Non-Interactive Process Path)
- Added the structured process contracts and system runner under
  `ArbSh.Core/Processes` without an intermediate command shell.
- Preserved a dedicated ordered argv view in the parser for external commands.
- Routed commands unresolved as Arabic built-ins through the shared executor
  path used by Console and Terminal hosts.
- Connected line-oriented pipeline input/output, separate errors, redirection,
  exact child exit codes, launch failure, and cancellation.
- Added kill-on-close Windows Job Object ownership and verified cancellation of
  a real descendant, while exposing the active ownership mode in every result.
- Added Linux launch through `setsid`, effective-PATH preflight, explicit POSIX
  ownership reporting, and shared cancellation/root-exit descendant gates.
- Added Arabic terminal chrome with live folder/status bindings and larger,
  more legible terminal spacing without changing the logical/visual text split.
- Kept the Linux CI receipt, live streaming, PTY/ConPTY, and Qalam hosting as
  explicit subsequent work.

## Notes on Documentation Locations

- Changelog is maintained in `docs/CHANGELOG.md`.
- Architecture and planning state live in `ROADMAP.md` and this document.
- BiDi algorithm changes must be reflected in the specific `docs/BIDI_*_RULES_DESIGN.md` files.
