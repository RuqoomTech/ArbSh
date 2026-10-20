# ArbSh Development Roadmap

**Current Version:** 0.8.1-alpha (Phase 6 Kickoff)
**Status:** Phase 6 In Progress - Live Process Output Implemented
**Next Step:** Linux CI Receipt and Interactive PTY/ConPTY

This roadmap outlines the development phases for ArbSh - an Arabic-first command-line shell built on C#/.NET with PowerShell-inspired architecture and full Unicode BiDi compliance. 

## 🎯 Project Vision

ArbSh aims to be the premier Arabic-first shell environment and the ultimate companion terminal for the **Baa (لغة باء)** programming language. By building a custom hardware-accelerated GUI terminal, we bypass legacy console limitations to provide a flawless, Right-To-Left environment that:

- **Provides Native Arabic Support:** Commands and parameters in Arabic script, rendered perfectly.
- **Hosts the Baa Compiler:** Serves as the official environment to correctly execute and display the Arabic output of the Baa language compiler.
- **Guarantees Unicode BiDi Compliance:** Full UAX #9 bidirectional text algorithm implementation.
- **Features an Object Pipeline:** PowerShell-inspired object-based command pipeline.
- **Is Cross-Platform:** Avalonia UI / .NET-based compatibility (Windows, macOS, Linux).

## 📋 Development Phases

### ✅ Phase 1: Project Foundation (Completed)
**C# Project Setup, Core Object Pipeline Design, Documentation**

-[✅] C#/.NET solution and console project structure
- [✅] Core pipeline classes (`PipelineObject`, `CmdletBase`)
- [✅] Command discovery framework (`CommandDiscovery.cs`)
- [✅] Project documentation updates
- [✅] Git configuration for C# development

### ✅ Phase 2: Core Shell Framework (Completed)
**Basic Cmdlet Framework & Execution Engine**

- [✅] REPL (Read-Eval-Print Loop) implementation
- [✅] Advanced parser with quote handling and escape sequences
- [✅] Reflection-based parameter binding with type conversion
- [✅] Task-based concurrent pipeline execution
- [✅] Core cmdlets: `اطبع`, `مساعدة`, `الأوامر`
- [✅] File redirection support (`>`, `>>`, `2>`, `2>>`)
- [✅] Stream merging (`2>&1`, `1>&2`)
- [✅] Pipeline input binding (`ValueFromPipeline`)

### ✅ Phase 3: Advanced Parsing & Tokenization (Completed)
**Regex-Based Tokenizer with Arabic Support**

-[✅] Regex-based tokenizer replacing state machine approach
- [✅] Token type system (`TokenType` enum, `Token` struct)
- [✅] Advanced redirection parsing (input `<`, stream merging)
- [✅] Variable expansion with concatenation (`$var`)
- [✅] Subexpression parsing `$(...)` (recursive)
-[✅] Type literal parsing `[TypeName]`
- [✅] UTF-8 encoding resolution for Arabic text
- [✅] Arabic command name support via `[ArabicName]` attributes

### ✅ Phase 4: BiDi Algorithm UAX #9 Compliance & Advanced Execution (Completed)
**Full Unicode BiDi Algorithm Implementation & Advanced Shell Features**

- [✅] **P Rules:** Paragraph embedding level determination (P2-P3)
- [✅] **X Rules:** Complete explicit formatting code handling (X1-X10)
- [✅] **W Rules:** Complete weak type resolution implementation (W1-W7)
- [✅] **N Rules:** Bracket pair processing and neutral type resolution (N0-N2)
- [✅] **I Rules:** Implicit embedding level assignment for strong types (I1-I2)
- [✅] **L Rules:** Complete level-based reordering implementation (L1-L4)
- [✅] **Subexpression Execution:** `$(...)` command substitution
- [✅] **Type Literal Utilization:** `[TypeName]` type casting
- [✅] **Testing:** 70+ Unicode BidiTest.txt compliance tests passing

### ✅ Phase 5: The Custom GUI Terminal (COMPLETED)
**Abandoning legacy console limitations to build a standalone, hardware-accelerated GUI terminal using Avalonia UI.**

#### 5.1 GUI Framework Architecture
- [x] **Avalonia UI Setup:** Create the new `ArbSh.Terminal` graphical project.
- [x] **Decouple Executor:** Refactor `Executor.cs` to output to a Stream/Event system instead of `System.Console.WriteLine`.
- [x] **Typography:** Embed a high-quality Arabic coding font (e.g., Cascadia Code Arabic, Kashida) as the default terminal font.
- [x] **Accessible Readability:** Use larger high-contrast Arabic defaults and keyboard/mouse terminal zoom with tested size bounds.

#### 5.2 Native Text Rendering & Shaping
- [x] **HarfBuzz Integration:** Leverage Avalonia's Skia/HarfBuzz backend for pixel-perfect Arabic character shaping and ligatures.
- [x] **Visual vs. Logical Mapping:** Keep shell state in logical order and delegate visual BiDi/shaping to Avalonia text layout at the rendering boundary.
- [x] **Color & Theming Engine:** Implement a modern dark theme with ANSI escape sequence parsing for colored output.

#### 5.3 RTL Input & Cursor Management (The Core Blocker Solved)
- [x] **True RTL Cursor Positioning:** Implement a cursor that logically navigates RTL text correctly (bypassing legacy Windows `conhost` bugs).
- [x] **Input Buffer Management:** Handle keyboard events directly from the OS GUI, completely avoiding console encoding corruptions.
- [x] **RTL Prompt:** Pin the prompt (e.g., `أربش< `) cleanly to the right side of the window.

#### 5.4 Terminal Emulator Features
- [x] **Scrollback Buffer:** Implement UI virtualization to handle thousands of lines of output efficiently.
- [x] **Clipboard Support:** BiDi-aware Copy/Paste (ensuring copied text pastes correctly into external editors in logical order).

### 🧠 Phase 6: Baa Language & External Process Integration
**Ensuring ArbSh is the perfect host environment for the Baa compiler and general external processes.**

#### 6.0 Eco Host Contract
- [x] Register ArbSh as an independently released Eco ecosystem project.
- [x] Document the planned [`arbsh-host-v1`](docs/ARBSH_HOST_V1.md) ownership and admission boundary.
- [ ] Freeze the CLI, session, UTF-8 stream, exit-status, environment, working-directory, and cancellation contract.
- [ ] Keep Baa, Nazm, and Takween directly invokable; ArbSh must remain an optional developer-experience layer.

#### 6.1 Hosting the Baa Compiler
- [ ] **Compiler Output Rendering:** Ensure the terminal flawlessly displays the Arabic stdout/stderr produced by the Baa compiler.
- [x] **Script Execution:** `تشغيل برنامج.baa` compiles through `baa` into a temporary directory and runs the program; `تشغيل` alone runs the nearest Takween project through `تكوين تشغيل`, preserving exit codes and diagnostics.
- [ ] **Baa Interactive Mode:** Support dropping the shell into a Baa REPL session with proper state preservation.

#### 6.2 General Process Management (Pseudo-TTY)
- [x] **Filesystem Built-ins:** Added Arabic file/directory commands (`انتقل`, `المسار`, `اعرض`) with session-scoped working directory behavior.
- [x] **Windows Installer Context Menu:** Added installer packaging scripts that register `Open in ArbSh` and pass `--working-dir` from Explorer.
- [x] **External Commands:** Execute non-interactive system commands (`git`, `dotnet`, `node`) through the shared core used by both hosts.
- [x] **Process Pipeline:** Integrate line-oriented external stdin/stdout/stderr with the ArbSh object pipeline in both directions.
- [x] **Live Output Streaming:** Stream non-interactive UTF-8 `stdout` and `stderr` incrementally through the structured runner and ArbSh pipeline while preserving complete captured results.
- [ ] **Interactive Stream Handling:** Add incremental stdin, terminal resize, control signals, and background-job flow through PTY/ConPTY.
- [x] **Arabic Path Support:** Preserve Arabic working directories, executable/argument paths, spaces, and empty arguments when launching external tools.
- [x] **Structured Process Core:** Added reusable immutable request/result contracts, UTF-8 stream capture, exit/failure classification, and cancellation.
- [x] **Structured Launch:** Build non-interactive external processes from argv/cwd/environment instead of concatenated shell command strings.
- [x] **Executor Integration:** Resolve commands that are not built-ins and route them through the structured runner without changing built-in precedence.
- [x] **Windows Process-Tree Ownership:** Assign every launched process to a
  kill-on-close Job Object and verify cancellation leaves no descendant alive.
- [x] **Linux POSIX Process-Group Adapter:** Launch through `setsid` with direct
  argv, verify the new session before continuing, and terminate the whole group.
- [ ] **Linux Ownership Receipt:** Prove cancellation and normal-root-exit
  descendant cleanup in Linux CI; macOS retains the visible transitional mode.
- [ ] **Qalam Host Parity:** Run the same ArbSh CLI through Qalam's PTY/ConPTY panel without embedding the Avalonia window.

### 🔧 Phase 7: Advanced Shell & Developer UX (Future)
**Polishing the developer experience.**

#### 7.1 Interactive UX
- [ ] **IntelliSense & Tab Completion:** Arabic-aware predictive text and auto-completion for commands, paths, and arguments.
- [ ] **Command History:** Persistent history (`سجل`) navigated with Up/Down arrows.
- [ ] **Multiline Input:** Support for writing control blocks or functions over multiple lines before execution.

#### 7.2 Advanced Scripting
- [ ] **User Variables:** Dynamic variable creation and management.
- [ ] **Functions:** User-defined function support with Arabic names.
- [ ] **Flow Control:** if/else, loops, switch statements with Arabic keywords.
- [ ] **Error Handling:** Beautiful, localized Arabic stack traces and error UI inside the terminal.

### 🌍 Phase 8: Deployment & Ecosystem (Future)
**Releasing the ArbSh/Baa environment to the world.**

- [ ] **Cross-Platform Binaries:** Package as standalone `.exe`/`.app` for Windows, Linux, and macOS (no runtime installation required).
- [ ] **Baa Package Manager (BPM) Interface:** Integrate commands to download and install Baa libraries from a central repository.
- [ ] **IDE Integration:** Visual Studio Code extension compatibility for ArbSh scripts.
- [ ] **Documentation Portal:** Generate an Arabic website documenting the shell features.
- [ ] **Standalone Installer Contract:** Pass install, upgrade, repair, health-check, and uninstall gates independently.
- [ ] **Developer Kit Admission:** Join Baa-Developer-Kit only after the standalone installer and Windows/Linux host gates pass.
- [ ] **Shared Arabic Corpus:** Publish `eco-arabic-text-corpus-v1` fixtures for Qalam, Pyramid-Engine, and a bounded future PyramidOS subset.

## 📊 Current Status Summary

### ✅ Phase 6 Progress (v0.8.1-alpha)
**Progress:** ArbSh now has foundational file-management commands, installer integration, and verified non-interactive external-command execution while preserving the Phase 5 GUI baseline and logical/visual separation architecture.

**Completed This Cycle:**
- Extracted engine code into `src_csharp/ArbSh.Core`.
- Introduced host-output abstractions (`IExecutionSink`, `CoreConsole`, `ShellEngine`) to preserve the logical/visual split and remove hard console coupling.
- Added `src_csharp/ArbSh.Terminal` Avalonia bootstrap (App/MainWindow/ViewModel/custom surface) as the foundation for full GUI terminal rendering.
- Implemented a dedicated terminal rendering pipeline (`TerminalTextPipeline`, `TerminalLayoutEngine`) for visual reordering/shaping of output and prompt lines.
- Added runtime font fallback configuration for mixed Arabic/Latin terminal text.
- Added renderer-focused tests for logical/visual separation and frame layout behavior.
- Implemented `TerminalInputBuffer` with logical caret, selection, grapheme-safe deletion, and insertion-at-caret editing.
- Added visual caret navigation for mixed BiDi input using `TextLine` hit-testing APIs.
- Added input selection with mouse drag and keyboard extension, plus clipboard copy/cut/paste integration.
- Anchored the Arabic prompt to RTL flow with the final marker form `أربش< `.
- Implemented scrollback offset virtualization with mouse-wheel and PageUp/PageDown navigation while keeping prompt pinned.
- Added output-line selection and clipboard copy in logical-order text, alongside existing prompt-line clipboard editing.
- Bundled terminal font assets (`CascadiaMono.ttf`, `arabtype.ttf`) and switched render font chain to packaged-first fallback.
- Replaced the Arabic fallback with the bundled Cairo font (SIL OFL) for Arabic runs, removed the non-redistributable `arabtype.ttf`, keeping Cascadia Mono for Latin text, and aligned all lines on one baseline.
- Mirrored the Windows Terminal window for Arabic: custom title bar, right-to-left tabs with independent sessions and Windows Terminal shortcuts, the Campbell palette, a prompt that follows output, and right-aligned output lines.
- Added full ANSI SGR parsing pipeline (16-color, 256-color, and truecolor) with span-based foreground/background styling.
- Added ArbSh navy theme/palette abstractions and applied ANSI-aware styling in output rendering without mutating logical text.
- Added tests for ANSI parser, ANSI palette mapping, and ANSI-aware terminal text pipeline behavior.
- Finalized the Arabic-only command surface, removed legacy aliases, and made
  `الأوامر` and bare `مساعدة` list every Arabic command with its description.
- Added metadata and smoke gates that require Arabic names/descriptions for all
  commands, Arabic names for every public parameter, and successful execution
  of every engine command.
- Added session-scoped working directory state and new file commands (`انتقل`, `المسار`, `اعرض`) with Arabic path support.
- Added terminal startup `--working-dir` handling to open ArbSh in a selected Explorer folder.
- Added Windows installer packaging scripts (`Install-ArbSh.ps1`, `Uninstall-ArbSh.ps1`) and release automation support for context-menu registration.
- Added immutable structured process requests/results with direct argv, cwd,
  environment, UTF-8 streams, exit/failure classification, and cancellation.
- Routed unresolved commands through the structured runner while preserving
  Arabic built-in precedence and the session working directory.
- Connected line-oriented ArbSh pipelines and existing stdout/stderr
  redirection to external processes in both directions.
- Added real-process coverage for quoted executables, Arabic paths, empty and
  option arguments, exit codes, launch failure, cancellation, and redirection.
- Added explicit Windows Job Object ownership with kill-on-close semantics,
  fail-closed ownership errors, a visible ownership mode, and a real spawned
  descendant cancellation gate.
- Added a Linux `setsid` adapter that creates the POSIX session before the
  target executes, preserves missing-executable classification through
  effective-PATH preflight, and runs the same descendant gates in Linux CI.
- Reworked ArbSh.Terminal chrome with an Arabic brand header, compact live
  working-directory panel, execution status, shortcut footer, larger terminal
  typography, and two focused GUI contract tests.

**Next Focus:** Obtain the Linux process-group receipt, then introduce the
interactive PTY/ConPTY path with incremental stdin, resize, terminal control
flow, and background jobs. Incremental stdout/stderr for non-interactive tools
is now complete.

## 🌟 Project Philosophy

**ArbSh is designed as an Arabic-first shell for the Arabic developer community.** Our approach prioritizes:

- **Cultural Authenticity:** Built by Arabic developers for Arabic developers.
- **The Perfect Host for Baa:** Acting as the definitive visual and interactive environment for the Baa programming language.
- **Technical Excellence:** Modern C# architecture with strict Unicode UAX #9 compliance.
- **Innovation:** Bypassing 40-year-old legacy console constraints to pioneer true Arabic-native command-line computing via a modern UI framework.
