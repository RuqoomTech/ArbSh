# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Eco Ecosystem Membership**: Registered ArbSh as the independently released
  Arabic-first shell and terminal host for the Baa ecosystem.
- **Host Contract Plan**: Added `docs/ARBSH_HOST_V1.md` to define ownership,
  PTY/ConPTY hosting, structured process launch, cancellation, Qalam
  integration, installer admission, and cross-platform acceptance gates.
- **Cross-Platform CI**: Added Windows and Linux restore, package audit,
  Release build, and test gates using the .NET 10 SDK.
- **Structured Process Contract**: Added immutable request/result contracts and
  a reusable non-interactive runner using direct argv, explicit working
  directory and environment changes, UTF-8 stdin/stdout/stderr, exit codes,
  launch-failure classification, and cancellation without an intermediate
  command shell.
- **Process Contract Fixtures**: Added a dedicated helper executable and ten
  tests covering Arabic paths, spaces, empty and shell-metacharacter arguments,
  clean/inherited environments, separated output streams, exit codes, launch
  failures, and cancellation.
- **External Command Execution**: Routed commands not resolved as Arabic
  built-ins through the structured runner with session working-directory
  inheritance, ordered argv, line-oriented pipeline stdin/stdout/stderr, and
  existing shell redirection behavior.
- **External Command Integration Tests**: Added real-process tests for quoted
  executables, empty/Arabic arguments, built-in precedence, bidirectional
  pipelines, separate stream redirection, exact child exit codes, launch
  failure code 127, and cancellation code 130.
- **Windows Process-Tree Ownership**: Added kill-on-close Windows Job Object
  ownership, explicit ownership-mode reporting, and fail-closed ownership
  failure classification.
- **Descendant Cancellation Gate**: Extended the process fixture to spawn a
  real child and verify cancellation leaves no surviving descendant.
- **Linux POSIX Ownership**: Added direct-argv launch through util-linux
  `setsid`, session verification, process-group termination, and effective-PATH
  executable preflight without an intermediate shell.
- **Background Descendant Gate**: Added coverage proving a normally exited root
  cannot leave a background child alive or keep inherited output pipes open.
- **Arabic Terminal Chrome**: Added an Arabic brand header, ellipsized live
  working-directory panel, execution status, keyboard-hint footer, larger text,
  and a compact two-line welcome experience.
- **Terminal Chrome Tests**: Added model tests for the welcome surface, current
  folder updates, and execution status transitions.
- **ArbSh Application Identity**: Added an original Arabic `أ` plus terminal
  cursor icon as transparent PNG and multi-resolution ICO assets, and applied
  it to the Avalonia header, window, published executable, and shortcuts that
  use the executable icon.
- **Live Process Output Contract**: Added typed stdout/stderr chunks, a
  thread-safe streaming sink boundary, and a streaming runner extension that
  still returns the complete captured process result.
- **Live Pipeline Delivery**: The executor now consumes its final pipeline
  stage while producers are running, so complete stdout/stderr lines reach the
  Console and Avalonia hosts before the child process exits.
- **Live Stream Gates**: Added deterministic process and end-to-end tests that
  hold a child open until its first Arabic output is observed. The complete
  Windows suite now passes 161 tests.
- **Accessible Terminal Typography**: Raised the default terminal font from 17
  to 20 logical pixels with a 32-pixel line height, preferred modern Arabic UI
  fallbacks, strengthened chrome and terminal contrast, and enlarged status
  text for high-DPI displays.
- **Terminal Zoom**: Added bounded `Ctrl++`, `Ctrl+-`, `Ctrl+0`, and Ctrl+wheel
  zoom that rebuilds BiDi caret, selection, and scrollback layout snapshots.
- **Visibility Gates**: Added default-size, zoom-bound, line-spacing, and WCAG
  normal-text contrast checks. The complete Windows suite now passes 164 tests.
- **Arabic Command Catalog**: Bare `مساعدة` and `الأوامر` now enumerate every
  Arabic command with a concise Arabic description and point directly to
  command-specific help; the terminal welcome names both discovery paths.
- **Arabic Surface Gates**: Every cmdlet must now provide an Arabic name and
  description, every public parameter must provide an Arabic name, and smoke
  tests execute every engine command without exposing legacy English aliases.
  The complete Windows suite now passes 175 tests.
- **`تشغيل` for Baa and Takween**: `تشغيل ملف.baa [وسائط]` compiles a single
  Baa file with `baa -o` into a private temporary directory, runs it with the
  remaining arguments, and deletes it afterwards; `تشغيل [مجلد] [وسائط]` runs
  the nearest `مشروع.تكوين` project (or the given one) through
  `تكوين تشغيل` from its root. Diagnostics stream unchanged, a failed compile
  stops before the run, and the program's or Takween's exit code becomes the
  shell's. Thirteen planner and executor tests, plus a CI job that builds the
  pinned Baa, Nazm, and Takween and runs real files and projects on Windows and
  Linux.

### Changed
- **Windows Terminal Layout, Mirrored for Arabic**: The Avalonia host now
  draws its own title bar with right-to-left tabs, a new-tab button, a menu,
  and left-side caption buttons. It adds independent tab sessions with the
  Windows Terminal shortcuts, the Campbell palette, a `أربش C:\path> ` prompt
  that follows the last output line, and right alignment for every output line.
- **Cairo for Arabic Text**: Bundled the Cairo font (SIL OFL 1.1). Arabic
  runs and the neutrals between Arabic words now render in Cairo, while Latin
  text stays in Cascadia Mono. Lines share one baseline. Removed the unused,
  non-redistributable `arabtype.ttf`.
- **.NET 10 LTS Baseline**: Migrated every project from .NET 9 to .NET 10 and
  pinned the supported SDK feature band in `global.json`.
- **Central Build Metadata**: Moved the target framework, language version,
  nullable settings, deterministic builds, and product version into
  `Directory.Build.props`.
- **Release Automation**: Release publishing now targets .NET 10 explicitly
  and stamps both hosts with the requested release version.
- **Avalonia Patch Level**: Updated Avalonia from 11.3.6 to 11.3.20 so the
  .NET 10 baseline no longer restores the vulnerable D-Bus dependency chain.
- **Process Fixture Signals**: Publish readiness files atomically and only
  after flushed output, eliminating races between tests and fixture writers.
- **Command Discovery Concurrency**: Build the reflection command catalog once
  through a thread-safe lazy snapshot so simultaneous first commands cannot
  corrupt the cache or register the same Arabic name twice.
- **ICU4N Patch Level**: Updated ICU4N from 60.1.0-alpha.437 to
  60.1.0-alpha.440 for the current SDK toolchain.
- **Invocation Parsing**: Preserved token positions and a dedicated ordered
  invocation-argument view so external options are not mistaken for ArbSh
  cmdlet parameters and quoted executable paths remain intact.
- **Host Failure Codes**: Reserved temporary code 125 for process-tree
  ownership failure while retaining 127 for launch failure and 130 for
  cancellation until `arbsh-host-v1` is frozen.

### Fixed
- **Pipeline Error Classification**: Preserved existing `PipelineObject`
  metadata emitted by cmdlets instead of wrapping error records as stdout.
- **.NET 10 Build Warnings**: Updated clipboard usage, nullable test state, and
  analyzer-sensitive assertions for a clean Release build.

## [0.8.1-alpha] - 2026-02-26
### Added
- **Arabic Command Surface Tests**: Added `ArabicCommandSurfaceTests` to assert active Arabic commands resolve and legacy English/old Arabic names do not resolve.
- **File Management Cmdlets**: Added `انتقل`, `المسار`, and `اعرض` for Arabic-first directory navigation and listing.
- **File Command Tests**: Added `FileManagementCmdletTests` for session directory switching, hidden file filtering, and Arabic path behavior.
- **Installer Scripts**: Added `installer/Install-ArbSh.ps1` and `installer/Uninstall-ArbSh.ps1` for per-user Windows installation and cleanup.

### Changed
- **Arabic-Only Command Resolution**: Command discovery now exposes only `[ArabicName]` command names.
- **Arabic Command Set Refresh**: Active command names are now `الأوامر`, `مساعدة`, `اطبع`, `انتقل`, `المسار`, `اعرض`, `اختبار-مصفوفة`, and `اختبار-نوع`; host exit command is `اخرج`.
- **Named Parameters in Arabic**: Named parameter binding now prefers Arabic parameter names only for user-facing cmdlets.
- **Documentation Command Examples**: Updated roadmap/readme/usage examples to reflect the Arabic-first command surface.
- **Session Working Directory**: `ShellEngine` now supports persistent `ShellSessionState` so file commands preserve directory context across commands.
- **Terminal Startup Directory**: `ArbSh.Terminal` now accepts `--working-dir` and initializes session directory from Explorer/context-menu launches.
- **Release Automation**: `create-release.ps1` now supports `-CreateInstaller` to package terminal app + installer scripts.

### Fixed
- **Legacy Alias Leakage**: Removed runtime discovery of legacy English and previous Arabic command aliases.
- **Relative Redirection Paths**: Executor redirections now resolve relative to session working directory instead of implicit process directory.

## [0.8.0-alpha] - 2026-02-26

### Added
- **ArbSh.Core Project**: Extracted the shell engine into a reusable class library (`src_csharp/ArbSh.Core`) containing parser, executor, cmdlets, models, and BiDi/i18n logic.
- **Host Output Abstraction**: Added `IExecutionSink`, `CoreConsole`, and `ShellEngine` to decouple command execution from terminal presentation.
- **ArbSh.Terminal Project**: Added a new Avalonia GUI host (`src_csharp/ArbSh.Terminal`) with app bootstrap, window/view model, and a custom `TerminalSurface` rendering entry point.
- **Phase 5.2 Rendering Pipeline**: Added `TerminalTextPipeline`, `TerminalLayoutEngine`, and render instruction models for output and prompt rendering.
- **Renderer Test Coverage**: Added terminal rendering tests in `ArbSh.Test` for pipeline transformation, visible frame windowing, and logical/visual separation.
- **Phase 5.3 Input Subsystem**: Added `TerminalInputBuffer`, `SelectionRange`, and `PromptLayoutSnapshot` for logical input editing, caret mapping, and selection geometry.
- **Input Test Coverage**: Added `TerminalInputBufferTests` for grapheme-safe delete/backspace, selection replacement, and caret movement boundaries.
- **Phase 5.4 Scrollback/Output Selection Models**: Added `TerminalFrameLayout` and `OutputSelectionBuffer` to support viewport windowing metadata and logical-order output selection/copy.
- **Phase 5.4 Test Coverage**: Added `OutputSelectionBufferTests` and new `TerminalLayoutEngine` scrollback-offset tests.
- **Phase 5 Typography Assets**: Added bundled terminal fonts under `src_csharp/ArbSh.Terminal/Assets/Fonts` and wired them as Avalonia resources.
- **ANSI Rendering Model**: Added `AnsiSgrParser`, `AnsiColorSpec`, `AnsiStyleState`, `AnsiStyleSpan`, `ParsedTerminalText`, `AnsiPalette`, and `TerminalTheme`.
- **ANSI Test Coverage**: Added `AnsiSgrParserTests` and `AnsiPaletteTests`.

### Changed
- **Namespace Split**: Migrated engine namespaces from `ArbSh.Console.*` to `ArbSh.Core.*` for strict separation between core logic and host UI.
- **Console Host Refactor**: Updated `ArbSh.Console` to consume `ArbSh.Core` and route output through sink-based host boundaries.
- **Test Project Alignment**: Updated `ArbSh.Test` references/imports to validate the extracted `ArbSh.Core` engine.
- **Solution Composition**: Updated `ArbSh.sln` to include `ArbSh.Core` and `ArbSh.Terminal`.
- **TerminalSurface Rendering Path**: Replaced inline drawing/reordering logic with a dedicated render pipeline and runtime font fallback chain.
- **TerminalSurface Interaction Model**: Upgraded keyboard/mouse input handling to support visual caret navigation, selection extension, and clipboard operations.
- **Prompt Token Style**: Updated GUI prompt marker to RTL-oriented form `أربش< `.
- **Test Project Metadata**: Set `<IsTestProject>true</IsTestProject>` in `ArbSh.Test.csproj` so `dotnet test` executes tests instead of skipping.
- **Terminal Scrollback UX**: `TerminalSurface` and `TerminalLayoutEngine` now support scrollback offsets, mouse-wheel scrolling, and PageUp/PageDown navigation while keeping the prompt pinned at the bottom.
- **Clipboard Priority Rules**: `Ctrl+C` now copies output-history selections first (logical order) and falls back to prompt-input selection copy.
- **Terminal Font Resolution**: `TerminalRenderConfig` now prioritizes packaged fonts before system fallback families.
- **Terminal Text Pipeline**: `TerminalTextPipeline` now strips ANSI escapes at render time and emits style spans while preserving logical source text.
- **Output Renderer Path**: `TerminalSurface` output drawing now applies per-span ANSI foreground/background/style formatting.

### Fixed
- **Engine/Host Coupling**: Removed direct dependence of core execution flow on `System.Console` output paths, enabling GUI-hosted rendering pipelines.
- **Logical/Visual Boundary Discipline**: Enforced visual transformation only in the terminal rendering layer while preserving logical strings in view-model state.
- **Broken Arabic Display in Avalonia Host**: Removed manual BiDi reordering before Avalonia text layout to prevent double reordering and reversed Arabic output.
- **Prompt Caret Misalignment (RTL)**: Updated terminal cursor placement to respect paragraph direction so caret aligns with RTL prompt/input flow.
- **Mixed BiDi Caret Placement**: Switched prompt caret position calculation to `TextLayout` hit-testing to align correctly for Arabic, English, and mixed prompt input.
- **Selection and Cursor Drift in Mixed Text**: Unified caret/selection logic on `TextLine` hit-testing to keep editing behavior stable across Arabic/English segments.
- **Scrollback Drift on Incoming Output**: Preserved reader position when new lines arrive while user is scrolled up in history.
- **ANSI Escape Leakage**: ANSI control sequences no longer appear as literal text in rendered output.

## [0.7.7.11] - 2025-07-03

### Added
- **Type Literal Utilization**: Complete implementation of PowerShell-style `[TypeName]` type literal functionality
  - Implemented `ProcessTypeLiterals` method to extract and map type literals to positional arguments
  - Added `TypeLiteralContext` class with `ArgumentTypeOverrides` and `ParameterPositionToArgumentIndex` mappings
  - Enhanced `ResolveTypeName` method with comprehensive type alias support (int, string, bool, double, datetime, ConsoleColor, etc.)
  - Integrated type literal processing with parameter binding system for automatic type conversion
  - Added support for multiple type literals in single command (e.g., `[int] 42 [string] hello [bool] true`)
  - Type literals properly excluded from positional parameter counting
  - Enhanced type conversion with `TypeConverter` and `Convert.ChangeType` fallback mechanisms
  - Successfully tested with various type combinations and complex parameter scenarios

### Technical Details
- Type literals parsed as `"TypeLiteral:TypeName"` strings and processed during parameter binding
- `ProcessTypeLiterals` creates proper argument index mapping excluding type literal positions
- Type resolution supports both common aliases (int, string, bool) and fully qualified names
- Parameter binding enhanced to use type literal overrides when available
- Maintains backward compatibility with existing parameter binding for non-type-literal arguments
- Comprehensive debug logging for type literal processing and parameter binding
- Type conversion handles enum types (ConsoleColor), DateTime parsing, and numeric conversions

## [0.7.7.10] - 2025-07-03

### Added
- **Subexpression Execution Logic**: Complete implementation of PowerShell-style `$(...)` command substitution
  - Implemented `ExecuteSubExpression` method with full pipeline execution for subcommands
  - Added subexpression support in parameter binding for positional parameters
  - Added subexpression support in array parameter binding
  - Added subexpression execution for unused arguments (for side effects)
  - Subexpressions now execute in isolated pipeline with proper output capture
  - Results are converted to strings and integrated into parent command parameter binding
  - Full error handling and debugging output for subexpression execution
  - Successfully tested with various cmdlets including `Get-Command`, `Get-Help`, `Write-Output`, and `Test-Array-Binding`

### Technical Details
- Subexpression execution uses temporary `BlockingCollection<PipelineObject>` for output capture
- Maintains same task-based concurrent execution model as main pipeline
- Proper cmdlet lifecycle management (BeginProcessing, ProcessRecord, EndProcessing)
- Type conversion support for subexpression results to target parameter types
- Comprehensive debug logging for subexpression execution flow
- Parameter binding logic updated to handle `List<ParsedCommand>` objects representing parsed subexpressions
- Array parameter binding enhanced to process subexpressions alongside string arguments

### Known Limitations
- Named parameter values containing subexpressions not yet supported (parser limitation)
- Nested subexpressions not fully tested but architecture supports them

## [0.7.7.9] - 2025-07-03

### Added
- **BidiTest.txt Testing Framework**: Complete Unicode conformance testing infrastructure
  - Downloaded and integrated official Unicode BidiTest-16.0.0.txt (490,846 test cases)
  - Created comprehensive `BidiTestFramework.cs` with full parsing and test execution capabilities
  - Created `BidiTestConformanceTests.cs` with multiple test methods for different test categories
  - Implemented test case parsing for @Levels, @Reorder, and data lines with bitset encoding
  - Added bidi class mapping from test format to internal BidiCharacterType enum
  - Created test string generation using representative Unicode characters
  - Implemented level verification and reorder verification logic
  - Added robust file path resolution for test execution in different environments
  - All conformance tests passing - BiDi algorithm demonstrates excellent UAX #9 compliance

### Technical Details
- BidiTest.txt format parsing supports @Levels, @Reorder directives and data lines
- Paragraph level bitset encoding (1=auto-LTR, 2=LTR, 4=RTL) fully implemented
- Level verification handles 'x' values for undefined/ignored levels correctly
- Visual reordering verification validates L2 rule implementation
- Representative Unicode character mapping covers all bidi classes (L, R, AL, EN, AN, etc.)
- Test framework integrates seamlessly with existing xUnit infrastructure
- ConvertRunsToLevels method bridges BidiRun list to levels array for verification
- Comprehensive error reporting and test categorization for debugging

## [0.7.7.8] - 2025-07-03

### Enhanced
- **BiDi P Rules (P2-P3)**: Enhanced paragraph embedding level determination with full UAX #9 compliance
  - Added proper isolate content skipping in P2 rule implementation
  - Added embedding initiator ignoring while processing characters within embeddings
  - Added nested isolate handling with depth tracking
  - Added unmatched isolate handling (skip to end of paragraph)
  - Added comprehensive P rules design documentation (BIDI_P_RULES_DESIGN.md)
  - Enhanced existing `DetermineParagraphLevel` method with UAX #9 compliant P2/P3 logic
  - Added helper methods for embedding initiator detection and text-based PDI matching
  - All 84 tests passing including existing P rules auto-detection scenarios

### Technical Details
- P2 rule now properly skips characters between isolate initiators (LRI, RLI, FSI) and matching PDI
- P2 rule correctly ignores embedding initiators (LRE, RLE, LRO, RLO) but processes characters within embeddings
- P3 rule maintains correct paragraph level assignment (0 for L, 1 for AL/R, 0 default)
- Enhanced Unicode handling with proper surrogate pair support
- Maintains backward compatibility with existing explicit level specification

## [0.7.7.7] - 2025-07-03

### Added
- **BiDi Algorithm L Rules (L1-L4)**: Complete implementation of UAX #9 L rules for final reordering and display
  - L1: Level reset for separators and trailing whitespace to paragraph level
  - L2: Progressive reversal from highest to lowest odd level with proper run segmentation
  - L3: Combining marks handling (placeholder for rendering-dependent implementation)
  - L4: Character mirroring for RTL contexts with comprehensive mirrored character pairs
- **Enhanced ReorderRunsForDisplay Method**: Complete rewrite with proper UAX #9 compliance
  - Supports paragraph level parameter for correct L1 level reset
  - Maintains backward compatibility with existing method signatures
  - Proper run splitting and level management for complex text scenarios
- **L Rules Unit Tests**: Added 6 comprehensive tests covering all L rules functionality
  - Simple RTL reversal verification
  - Mixed LTR/RTL text handling
  - Character mirroring in RTL contexts
  - Edge cases (empty text, null runs)

### Technical Details
- All 76 unit tests passing (70 existing + 6 new L rules tests)
- Complete UAX #9 L rules compliance for final text reordering
- Proper Unicode character mirroring with escape sequences for compatibility
- Enhanced run splitting algorithms for complex level reset scenarios

## [0.7.7.6] - 2025-07-03

### Fixed
- **BiDi Algorithm I Rules**: Corrected implementation of UAX #9 I1 and I2 rules for implicit embedding level resolution
  - I1 Rule: Fixed EN characters with even levels to increase by 2 levels (not 1)
  - I2 Rule: Fixed AN characters with odd levels to increase by 1 level (was missing)
  - Added proper BN (Boundary Neutral) character handling per UAX #9 specification
  - Added level overflow protection for max_depth+1 (126) limit
- **Test Coverage**: Updated and added 8 new comprehensive I rules unit tests
  - Fixed existing W2 test to match corrected I1 behavior for AN characters
  - Added tests for all I1/I2 rule combinations with proper RTL/LTR contexts

### Added
- **Documentation**: Created comprehensive `docs/BIDI_I_RULES_DESIGN.md` design document
  - Detailed UAX #9 I rules specification analysis
  - Implementation architecture and algorithm design
  - Testing strategy and performance considerations
- **Run Segmentation Tests**: Added 5 new tests for run segmentation logic verification
  - Mixed LTR/RTL text segmentation
  - Explicit formatting character handling
  - Numeric sequences in different contexts
  - Edge cases (empty text, single character)

### Technical Details
- All 70 unit tests passing (62 existing + 8 new)
- Proper UAX #9 compliance for implicit level resolution
- Enhanced error handling and boundary condition management

## [0.7.7.5] - 2025-07-03

### Added

- **BiDi Algorithm Enhancement (UAX #9 N Rules - Complete Implementation):**
  - **Neutral Type Resolution (N0-N2):** Implemented complete UAX #9 N rules for resolving neutral character types:
    - N0: Bracket pair processing with BD16 algorithm using 63-element stack for proper bracket pairing
    - N1: Neutral sequence resolution based on surrounding strong types (treating EN/AN as R)
    - N2: Embedding direction fallback for remaining neutrals (even level → L, odd level → R)
  - **Bracket Pair Processing:** Implemented comprehensive bracket pair identification and resolution:
    - BD14-BD16 definitions for opening/closing paired brackets with ON type requirement
    - Hardcoded bracket mapping table (fallback for missing ICU4N bracket properties)
    - Support for basic brackets (), [], {} and Unicode brackets ⟨⟩, 〈〉, ⟦⟧, ⟪⟫, ⦃⦄, ⦅⦆
    - Canonical equivalence handling for angle brackets (U+3008/U+3009 ↔ U+2329/U+232A)
    - Sequential processing in logical order of opening brackets
    - NSM character handling for brackets that change type under N0
  - **Neutral Type Context Analysis:** Implemented sophisticated neutral type resolution:
    - Strong type context establishment for bracket pair resolution
    - Embedding direction matching and opposite direction context checking
    - Boundary type (sos/eos) integration for isolating run sequence boundaries
    - Proper handling of mixed strong types within bracket pairs
  - **Integration with Existing Pipeline:** Seamless integration with W and X rules processing:
    - Reuse of isolating run sequence infrastructure from W rules
    - Proper type propagation back to main character type array
    - Maintained compatibility with existing I rules processing
  - **Comprehensive Testing:** Added 4 new unit tests covering N rules functionality:
    - N0 bracket pair tests for LTR and RTL contexts
    - N1 surrounding strong type resolution tests
    - N2 embedding direction fallback tests
    - All 57 tests passing (53 existing + 4 new N rules tests)

## [0.7.7.2] - 2025-07-03

### Added

- **BiDi Algorithm Enhancement (UAX #9 W Rules - Complete Implementation):**
  - **Weak Type Resolution (W1-W7):** Implemented complete UAX #9 W rules for resolving weak character types:
    - W1: NSM (Non-Spacing Mark) resolution based on previous character or sos
    - W2: EN to AN conversion in Arabic Letter context with backward strong type search
    - W3: AL to R simplification for consistent strong type handling
    - W4: Number separator resolution (ES between EN, CS between same number types)
    - W5: European terminator sequences adjacent to European numbers
    - W6: Remaining separator cleanup to Other Neutral
    - W7: EN to L conversion in Latin context with backward strong type search
  - **Isolating Run Sequence Processing:** Implemented proper isolating run sequence construction and processing:
    - IsolatingRunSequence class with Types, Positions, Sos, and Eos properties
    - Maximal sequence building connected by isolate initiators and matching PDIs
    - Start-of-sequence (sos) and end-of-sequence (eos) type determination
    - Proper integration with existing X rules processing
  - **Enhanced I Rules:** Extended I1-I2 rules for final embedding level assignment:
    - I1: Even level + (R or AN) → next higher odd level
    - I2: Odd level + (L or EN) → next higher even level
    - Proper level assignment for Arabic Numbers (AN) as RTL characters
  - **Helper Methods:** Added comprehensive helper methods for W rules processing:
    - Strong type searching (backward search for L, R, AL, or sos)
    - Character type classification (strong, number, isolate types)
    - Isolate initiator and PDI detection and matching

- **BiDi Algorithm Enhancement (UAX #9 X Rules - Complete Implementation):**
  - **Basic Explicit Formatting (X1-X4, X6-X8):** Implemented complete UAX #9 X rules for explicit directional formatting characters:
    - X1: Main processing loop with character-by-character rule application
    - X2: RLE (Right-to-Left Embedding) with odd level calculation and stack management
    - X3: LRE (Left-to-Right Embedding) with even level calculation and stack management
    - X4: RLO (Right-to-Left Override) with type forcing to R and override status tracking
    - X5: LRO (Left-to-Right Override) with type forcing to L and override status tracking
    - X6: General character processing with directional override application
    - X7: PDF (Pop Directional Formatting) with proper stack management and level restoration
    - X8: End of paragraph processing with stack reset
  - **Isolates Support (X5a-X5c, X6a):** Implemented complete isolate processing with proper pairing:
    - X5a: LRI (Left-to-Right Isolate) with even level calculation and isolate flag
    - X5b: RLI (Right-to-Left Isolate) with odd level calculation and isolate flag
    - X5c: FSI (First Strong Isolate) with automatic direction detection via first-strong character scanning
    - X6a: PDI (Pop Directional Isolate) with isolate-aware stack popping and proper matching
  - **Stack Overflow Protection:** Implemented UAX #9 compliant maximum embedding depth limit of 125 levels
  - **Directional Status Stack:** Added `DirectionalStatusStackEntry` struct with embedding level, override status, and isolate status tracking
  - **First Strong Detection:** Implemented sophisticated first-strong character scanning for FSI with nested isolate boundary respect

### Changed

- **BiDi Algorithm Core:** Completely rewrote `BidiAlgorithm.ProcessRuns` method from placeholder logic to full UAX #9 X rules compliance
- **Data Structures:** Enhanced directional processing with new `DirectionalOverrideStatus` enum (Neutral, LeftToRight, RightToLeft)
- **Unit Testing:** Expanded test suite from 40 to 46 comprehensive unit tests covering all X rules scenarios:
  - Basic embedding tests (RLE, LRE with proper level calculation)
  - Override tests (RLO, LRO with type forcing verification)
  - Isolate tests (LRI, RLI, FSI with direction detection)
  - Edge cases (unmatched PDF/PDI, nested structures, stack overflow protection)
  - All tests passing with 100% success rate

### Documentation

- **Design Documentation:** Created comprehensive `docs/BIDI_X_RULES_DESIGN.md` documenting:
  - Complete UAX #9 X rules research and analysis
  - Implementation strategy and architectural decisions
  - Data structure design rationale
  - Phase-by-phase implementation plan
- **Technical Specifications:** Documented all X rules (X1-X8) with implementation details and Unicode compliance notes

## [0.7.7.1] - 2025-05-22

### Added

- **BiDi Algorithm Enhancement (Foundation):**
  - Integrated `ICU4N` library for accessing Unicode Character Database properties.
  - Refactored `BidiAlgorithm.GetCharType(int codepoint)` to use `ICU4N` for determining the UAX #9 `Bidi_Class` of Unicode codepoints, replacing previous hardcoded range checks. This provides significantly more accurate character type classification.
  - Updated `BidiCharacterType` enum to include `BN` (Boundary Neutral) and align more closely with UAX #9 terminology. LRM/RLM are now correctly classified as BN by `GetCharType`.

### Changed

- **Unit Tests (BiDi):** Updated and expanded unit tests for `BidiAlgorithm.GetCharType` to verify correct classification based on ICU data, including LRM, RLM (as BN), Tab (as S), and various other character types according to their UCD `Bidi_Class`. All `GetCharType` tests are now passing.
- Placeholder logic in `BidiAlgorithm.ProcessRuns` updated slightly to use the new `GetCharType` for its basic P2/P3 paragraph level detection. (Full `ProcessRuns` UAX #9 implementation is the next major task).

### Fixed

- Test failures in `BidiAlgorithmTests.cs` related to LRM/RLM classification are now resolved by updating tests to expect `BN` and refining `GetCharType`.

## [0.7.7] - 2025-05-22

### Added

- **Unit Testing (BiDi):** Introduced xUnit test project (`ArbSh.Test`).
- **Unit Testing (BiDi):** Added comprehensive unit tests for `BidiAlgorithm.GetCharType`, covering:
  - Basic character types (L, R, AL, EN, AN, WS, B).
  - Explicit directional formatting codes (LRE, RLE, PDF, LRO, RLO, LRI, RLI, FSI, PDI, LRM, RLM).
  - Other types (ES, CS, NSM, S, ON) and default classification logic.
- **Unit Testing (BiDi):** Added initial unit tests for `BidiAlgorithm.ProcessRuns` covering:
  - Purely LTR and purely RTL text scenarios with varying base paragraph directions.
  - Simple mixed LTR/RTL text.
  - Scenarios with explicit embedding codes (LRE, RLE, PDF) and nested embeddings. (Note: These tests revealed issues in `ProcessRuns` logic requiring further debugging).

### Changed

- **BiDi Algorithm:** Made an initial correction to `BidiAlgorithm.GetCharType` to prioritize `AN` (Arabic Number) check before the broader `AL` (Arabic Letter) check for overlapping Unicode ranges, resolving a test failure.
- **BiDi Algorithm:** Attempted refactoring of `BidiAlgorithm.ProcessRuns` to improve level assignment logic for runs, particularly around explicit formatting codes. (Note: This refactoring introduced regressions, currently under investigation).

### Fixed

- Corrected a misclassification in `BidiAlgorithm.GetCharType` where Arabic numbers were being identified as Arabic letters. Unit tests now confirm correct classification.

## [0.7.6] - 2025-05-07

### Added

- **Parser:** Added parsing support for type literals (e.g., `[int]`, `[string]`, `[System.ConsoleColor]`). Recognized via `TokenType.TypeLiteral` and stored as a special argument string (`"TypeLiteral:TypeName"`) in `ParsedCommand`.
- **Parser:** Added parsing support for input redirection (`< file.txt`). Recognized via `TokenType.Operator` (`<`) and stored in the new `InputRedirectPath` property of `ParsedCommand`.
- **BiDi Algorithm:** Ported core BiDi algorithm logic (determining character types, resolving embedding levels via explicit codes, reordering runs per Rule L2) from the original C implementation to `I18n/BidiAlgorithm.cs`. Includes `GetCharType`, `ProcessRuns`, `ReorderRunsForDisplay`, and `ProcessString` methods. (Note: Based on simplified C logic, requires testing and rendering integration).
- **Executor:** Implemented execution logic for input redirection (`< file.txt`). Reads specified file and provides content as pipeline input to the first command.
- **Executor:** Implemented execution logic for stream redirection merging (`2>&1`, `1>&2`). Output/Error objects are now correctly routed to the target stream's file handle or console stream based on merge directives.

### Fixed

- **Parser:** Correctly interpret standard escape sequences (`\n`, `\t`, `\"`, `\\`, `\$`, etc.) within double-quoted strings (`StringLiteralDQ`). Added `ProcessEscapesInString` helper method. Single-quoted strings remain literal.
- **Parser:** Fixed sub-expression (`$(...)`) parsing regression. Nested sub-expressions and pipelines within them are now parsed correctly without causing "unterminated" errors or incorrect token consumption.
- Fixed build warnings related to unused variables in `Executor.cs`'s output handling logic.
- Fixed build warning related to empty switch block in `I18n/BidiAlgorithm.cs`.

### Changed

- Updated `README.md` and `ROADMAP.md` to reflect completion of Phase 3 parsing enhancements and Phase 4 progress (BiDi porting, redirection execution).

## [0.7.5] - 2025-05-06

### Fixed

- **Encoding Issues:** Resolved persistent UTF-8 input/output corruption when running via PowerShell `Start-Process` with redirected streams. This involved:
  - Setting `StandardInputEncoding`, `StandardOutputEncoding`, and `StandardErrorEncoding` to UTF-8 in `test_features.ps1`.
  - Modifying `Program.cs` to use `StreamReader` with explicit UTF-8 encoding for `Console.OpenStandardInput()` and ensuring `Console.OutputEncoding` is also UTF-8.
  - Arabic commands/parameters and output are now handled correctly in test scenarios.
- **Variable Expansion Regression:** Fixed the parser logic in `Parser.cs` to correctly handle variable expansion (`$var`) within arguments, ensuring adjacent tokens are concatenated properly (e.g., `ValueIs:$testVar` now works as expected). Implemented using a `StringBuilder` in the argument parsing loop.

### Changed

- Updated `README.md` and `docs/USAGE_EXAMPLES.md` to reflect the encoding and variable expansion fixes.
- Updated `ROADMAP.md` to mark the encoding blocker as resolved and the variable expansion fix as complete.
- **Tokenizer Refactoring:** Replaced the internal state-machine tokenizer with a new Regex-based tokenizer (`Parsing/RegexTokenizer.cs`) using Unicode properties (`\p{L}`) for potentially better handling of mixed-script identifiers and complex syntax elements. Created `Parsing/Token.cs` for token definitions. Integrated the new tokenizer into `Parser.cs`.
- **Parser Logic:** Updated redirection and argument/parameter parsing logic in `Parser.cs` to work with the new `List<Token>` structure.
- **Redirection Parsing:** Refined Regex patterns in `RegexTokenizer.cs` and parsing logic in `Parser.cs` to correctly identify and parse all standard redirection operators (`>`, `>>`, `2>`, `2>>`, `>&1`, `>&2`).
- **Executor Redirection Handling:**
  - Implemented handling for stdout file redirection (`>`, `>>`) in `Executor.cs`.
  - Implemented handling for stderr file redirection (`2>`, `2>>`) in `Executor.cs`.
  - Fixed null path error when no redirection was specified.
- **Error Handling:** Added `IsError` flag to `PipelineObject.cs` and updated `GetHelpCmdlet.cs` to use it for "command not found" errors.
- **Test Script:** Updated `test_features.ps1` to log temporary file contents line-by-line to avoid `Add-Content` stream errors. Changed default file encoding for `test_output.log` to UTF-8 with BOM.

### Known Issues / Regressions

- **Tokenizer:**
  - Input redirection operator `<` is not yet recognized.
  - Mixed-script identifiers (e.g., `Commandمرحبا`) need verification/improvement.
- **Parser:**
  - Sub-expression `$(...)` parsing is implemented, but execution is not.
  - Type literals `[int]` are not yet parsed.
- **Execution:**
  - Stream redirection merging (`2>&1`, `1>&2`) is parsed but not implemented in the Executor.
  - Sub-expression execution is not implemented.

## [0.7.0] - 2025-05-03

### Added

- **Arabic Command Name Support:**
  - Extended the parser (`Parser.cs`) to recognize Arabic letters as valid characters in command/argument tokens.
  - Added `ArabicNameAttribute.cs` to allow specifying an Arabic alias for a cmdlet class.
  - Updated `CommandDiscovery.cs` to read the `ArabicNameAttribute` and add both English and Arabic names to the command cache.
  - Cmdlets can now be invoked using either their English name (e.g., `Get-Help`) or their assigned Arabic name (e.g., `احصل-مساعدة`).
- **Arabic Parameter Name Support:**
  - Updated `ArabicNameAttribute` to be applicable to properties (parameters).
  - Modified the parameter binding logic in `Executor.cs` to check for `ArabicNameAttribute` on properties and attempt binding using the Arabic name first, falling back to the English name.
  - Cmdlet parameters can now be specified using either their English name (e.g., `-CommandName`) or their assigned Arabic name (e.g., `-الاسم`).
- **Testing:** Added tests to `test_features.ps1` for Arabic command/parameter aliases.

### Fixed

- **Parser Tokenization:** Corrected tokenizer logic (`Parser.cs`) to properly handle hyphens within command names (e.g., `Get-Command`) and dots within arguments/filenames (e.g., `temp_file.txt`), preventing incorrect splitting.
- **Redirection:** Resolved issue where output redirection (`>`, `>>`) failed due to incorrect filename parsing. Files are now created correctly. Added explicit `Flush()` calls in `Executor.cs` for robustness.
- **Test Script Encoding:** Fixed `test_features.ps1` to correctly send UTF-8 encoded input to the ArbSh process by writing bytes directly to the standard input stream, enabling proper testing of Arabic characters. Confirmed successful parsing and execution of Arabic aliases.

## [0.6.0] - 2025-05-02

### Changed

- **Pipeline Execution:** Refactored the `Executor` to use `System.Threading.Tasks.Task` and `System.Collections.Concurrent.BlockingCollection` for true concurrent execution of pipeline stages within a statement, replacing the previous sequential simulation. Each cmdlet now runs in its own task, improving potential parallelism. Includes basic error handling for task failures using `Task.WhenAll` and `AggregateException`.
- **Cmdlet Logic & Pipeline Binding:**
  - Enhanced `ParameterAttribute` to include `ValueFromPipeline` and `ValueFromPipelineByPropertyName` flags.
  - Added `BindPipelineParameters` virtual method to `CmdletBase` to handle binding pipeline input to parameters based on the new flags (called by Executor before `ProcessRecord`).
  - Updated `GetHelpCmdlet` to display pipeline input acceptance details for parameters when using the `-Full` switch.
  - Updated `WriteOutputCmdlet` to declare `ValueFromPipeline=true` for its `InputObject` parameter and handle processing pipeline input vs. direct parameter value.
  - Updated `GetCommandCmdlet` to output structured `Models.CommandInfo` objects instead of just strings.
  - Refined parameter binding in `Executor.cs` to throw specific `ParameterBindingException` on type conversion errors (named and positional) instead of just logging warnings.
  - Added support in `Executor.cs` for binding remaining positional arguments to a parameter with an array type (e.g., `string[]`).

## [0.5.0] - 2025-05-02

### Added

- **Parser Refinements (2025-05-01):**
  - Implemented basic variable expansion (`$varName`) during tokenization.
  - Implemented statement splitting (`;`) respecting quotes/escapes.
  - Implemented pipeline splitting (`|`) respecting quotes/escapes.
  - Implemented general escape character (`\`) handling (including inside quotes).
  - Updated `Executor` to process multiple statements sequentially.
- **Start of C#/.NET Refactoring:**
  - Created new C# solution (`src_csharp/ArbSh.sln`) and console project (`src_csharp/ArbSh.Console`).
  - Added placeholder classes for core pipeline (`PipelineObject`, `CmdletBase`) and execution (`Parser`, `Executor`).
  - Implemented basic REPL loop in `Program.cs`.
  - Added basic `WriteOutputCmdlet` placeholder.
  - Added `ParsedCommand.cs` to represent parsed command structure, including properties for output redirection.
  - Updated `Parser.cs`'s `TokenizeInput` method to handle escaped quotes (`\"`) and basic escaped operators (`\|`, `\;`, `\>`).
  - Updated `Parser.cs`'s `Parse` method to detect output redirection operators (`>`, `>>`) and store the path/append flag in `ParsedCommand`. Added basic handling for command separator (`;`) - currently only processes the first statement.
  - Added `ParameterAttribute.cs` for marking cmdlet parameters.
  - Updated `GetHelpCmdlet.cs` and `GetCommandCmdlet.cs` to use `[Parameter]` attribute.
  - Implemented parameter binding logic in `Executor.cs` using reflection, including mandatory parameter checks (throwing `ParameterBindingException`), stricter boolean switch handling, and improved type conversion using `TypeConverter` / `Convert.ChangeType`.
  - Added `CommandDiscovery.cs` to find available cmdlets using reflection.
  - Updated `Executor.cs` to use `CommandDiscovery`, handle `ParameterBindingException`, and use the refined binding logic.
  - Updated `CmdletBase.cs` to collect output internally using `BlockingCollection`.
  - Updated `Executor.cs` with basic sequential pipeline logic (passing output of one command to the input of the next via `BlockingCollection`), confirmed working with `Get-Command | Write-Output`.
  - Updated `Parser.cs` to handle the pipeline operator (`|`).
  - Implemented basic logic for `GetHelpCmdlet` using reflection to display command syntax and parameters.
  - Implemented basic logic for `GetCommandCmdlet` using `CommandDiscovery` to list commands.
- Updated documentation (`README.md`, `ROADMAP.md`, `docs/PROJECT_ORGANIZATION.md`, `docs/DOCUMENTATION_INDEX.md`, `CONTRIBUTING.md`, `docs/CHANGELOG.md`) to reflect the new C# direction and refactoring status.
- Updated `.gitignore` for C#/.NET development.

### Changed

- Project direction shifted towards a PowerShell-inspired C# shell with Arabic command support.
- Build system transitioned from CMake to .NET CLI.
- Moved original C/C++ source, include, tests, cmake, build files into `old_c_code/` directory for reference.

### Deprecated

- The existing C implementation (now in `old_c_code/`) is considered legacy and primarily serves as a reference for porting i18n logic.
- CMake build system (now in `old_c_code/`).

## Previous C Implementation [Unreleased] - Before C# Refactor

### Added

- ImGui-based GUI mode for Windows
  - Modern GPU-accelerated user interface
  - Tab-based terminal interface
  - Customizable appearance with dark mode
  - Command history and completion
  - Split testing configuration in CMake build system
  - Four distinct build modes (console, GUI, console+tests, GUI+tests)
  - Arabic text support in the GUI interface
- Unified entry point architecture through `shell_entry.c`
  - Single code path for shell initialization across all platforms
  - Improved maintainability and code organization
  - Simplified build process
- Complete implementation of Unicode Bidirectional Algorithm (UAX #9)
  - Full support for directional formatting characters (LRM, RLM, LRE, RLE, LRO, RLO, LRI, RLI, FSI, PDI)
  - Proper handling of nested bidirectional text
  - Paragraph-level direction control
  - Comprehensive character type classification
- Enhanced Arabic keyboard layout support
  - Complete key mapping for Arabic characters
  - Keyboard layout switching with 'layout' command
  - Ctrl+A shortcut for quick layout toggling
  - Visual indicators for current input mode

### Changed

- Enhanced UI appearance for ImGui shell
  - Modern blue color scheme with rounded corners
  - Improved tab styling and visibility
  - Better console text display with proper wrapping
  - Enhanced command input styling
  - Fullscreen window mode with proper layout
- Improved build system with GCC toolchain support
  - Added toolchain file for GCC compilation
  - Enhanced CMake configuration options
  - Better cross-platform build support
- Merged multiple entry point files (`main.c`, `main_gui.c`, `win_main.c`, `win_gui_common.c`) into a single `shell_entry.c`
- Updated CMakeLists.txt to use the new unified entry point
- Enhanced README.md with architecture documentation
- Improved integration between console and GUI modes
- Consolidated common platform-specific code
- Refactored bidirectional text handling for better performance and compliance with Unicode standards
- Enhanced cursor positioning logic for RTL text input

### Fixed

- ImGui integration issues
  - Fixed style push/pop imbalance causing assertion failures
  - Corrected InputText callback implementation
  - Properly initialized DirectX resources
  - Fixed window sizing and positioning
- Bidirectional text rendering issues with mixed Arabic and Latin text
- Arabic character input and display problems
- Cursor positioning in RTL text mode
- Status bar updates for keyboard layout changes

### Removed

- Redundant initialization code across multiple entry point files
- Duplicate functionality for Windows GUI mode

## [1.1.0] - 2025-03-02

### Added

- Comprehensive Arabic language support
  - Complete UTF-8 implementation
  - Right-to-left text display
  - Bidirectional text algorithm (partial implementation)
  - Arabic character shaping
  - Arabic input method support
- Baa language integration capabilities
  - Compatible terminal environment
  - Support for Arabic programming
  - Consistent cross-platform behavior
- Localization system with English and Arabic support
- `lang` command to switch between languages
- Custom Windows console for enhanced Arabic support
- Enhanced documentation
  - PROJECT_STATUS.md - Current implementation status
  - ARABIC_SUPPORT_GUIDE.md - Technical details of Arabic support
  - BAA_INTEGRATION.md - Integration with Baa language
  - DEVELOPMENT_TASKS.md - Prioritized development tasks
  - DOCUMENTATION_INDEX.md - Overview of all documentation

### Changed

- Refactored text handling to use UTF-8 throughout
- Updated README.md to reflect new focus on Arabic support and Baa integration
- Enhanced CONTRIBUTING.md with Arabic and Baa-specific guidelines
- Improved UI for bidirectional text display
- Restructured code for better modularity of language features

### Fixed

- Unicode rendering issues in Windows console
- Command parsing errors with RTL text
- Input positioning with bidirectional text
- File path handling with Arabic characters

## [1.0.1] - 2025-01-31

### Added

- Windows-specific signal handling with proper SIGINT definition
- CMake build system improvements for MSVC compiler
- Cross-platform compatibility layer in shell.h

### Changed

- Updated header file organization for better Windows/Unix compatibility
- Improved signal handler implementation for Windows
- Refactored environment variable handling for Windows compatibility

### Fixed

- Signal handling issues on Windows platform
- Build errors related to missing POSIX headers on Windows
- Type conversion warnings in getLine.c
- Environment variable access on Windows systems

## [1.0.0] - 2025-01-31

### Added

- Cross-platform support for Windows and Unix-like systems
- CMake build system with proper configuration
- Documentation generation using Doxygen
- Comprehensive .gitignore file
- Environment variable handling for both platforms
- Signal handling for Windows and Unix-like systems
- Built-in command support (cd, exit, env, etc.)
- History functionality
- Command chaining support (&&, ||, ;)
- Alias support
- Variable replacement ($? and $$)

### Changed

- Improved code organization and structure
- Enhanced error handling and reporting
- Updated function documentation
- Optimized environment variable management
- Standardized coding style across the project

### Fixed

- Duplicate function definitions
- Memory leaks in environment variable handling
- Signal handling issues on Windows
- Path resolution bugs
- Command execution errors
- History file handling issues

### Security

- Improved environment variable security
- Better memory management
- Enhanced error checking for system calls

## [0.1.0] - 2025-01-30

### Added

- Initial release
- Basic shell functionality
- Command execution
- Environment variable support
- Basic error handling
