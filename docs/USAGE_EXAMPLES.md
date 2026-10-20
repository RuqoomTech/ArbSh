# ArbSh - Usage Examples and Feature Guide

This document provides comprehensive examples for all implemented features in ArbSh - the Arabic-first shell.

**Current Version:** 0.8.1-alpha
**Status:** Phase 6 In Progress - Windows/Linux Process Ownership Implemented
**Next Step:** Linux CI Receipt, Live Streams, and PTY/ConPTY

## ✅ **Fully Implemented Features**
- `ArbSh.Core` shared engine extraction (host-agnostic parser/executor/cmdlets/BiDi)
- Sink-based execution boundary (`IExecutionSink`) between core logic and host rendering
- Avalonia GUI terminal project scaffold (`ArbSh.Terminal`)
- Terminal rendering pipeline for output/prompt (`TerminalTextPipeline` + `TerminalLayoutEngine`)
- Logical input engine with caret/selection state (`TerminalInputBuffer`)
- Mixed BiDi caret mapping via Avalonia text hit-testing
- Keyboard and mouse selection with clipboard copy/cut/paste for prompt input
- Scrollback viewport virtualization with mouse wheel and `PageUp`/`PageDown`
- Output history selection with logical-order copy to clipboard (`Ctrl+C`)
- Bundled terminal fonts with packaged-first Arabic/Latin fallback chain
- ANSI SGR color/style rendering (16-color, 256-color, and truecolor)
- Complete BiDi Algorithm (UAX #9) with all rule sets (P, X, W, N, I, L)
- Pipeline execution with task-based concurrency
- Parameter binding with reflection and type conversion
- Subexpression execution `$(...)` - **WORKING**
- Type literal utilization `[TypeName]` - **WORKING**
- Variable expansion `$variableName`
- Input/output redirection and stream merging
- Arabic command names
- Command discovery and caching
- Structured non-interactive external commands with direct argv and no
  intermediate system shell
- External/built-in text pipelines, separate stdout/stderr, redirection, and
  child exit-code preservation

## Running ArbSh

### Console Host (Current Stable REPL)
1. Navigate to the repository root.
2. Run `dotnet run --project src_csharp/ArbSh.Console`.
3. You should see the `ArbSh>` prompt. Type `اخرج` to quit.

### تشغيل البرامج الخارجية غير التفاعلية

أي أمر لا يطابق أمرًا عربيًا داخليًا يُمرر إلى نظام التشغيل عبر `argv` منظم،
ولا يُرسل إلى `cmd /c` أو صدفة وسيطة:

```powershell
ArbSh> dotnet --version
ArbSh> git status
ArbSh> dotnet build "مشروع عربي/مشروع.csproj"
ArbSh> اطبع "نص" | أداة-خارجية --خيار
ArbSh> أداة-خارجية > "خرج.txt" 2> "خطأ.txt"
```

تظل الأوامر العربية الداخلية أعلى أولوية من أي برنامج خارجي يحمل الاسم نفسه.
وتحفظ `ShellSessionState.LastExitCode` رمز البرنامج الحقيقي؛ ويستخدم أربش
مؤقتًا 127 لفشل الإطلاق و130 للإلغاء. البرامج التفاعلية والبث الحي تنتظر مسار
PTY/ConPTY في الخطوة التالية.

### Avalonia Terminal Host (Phase 5 Foundation)
1. Navigate to the repository root.
2. Run `dotnet run --project src_csharp/ArbSh.Terminal`.
3. Verify the window opens and the custom terminal surface renders output and prompt lines with Arabic-aware visual ordering.

### Avalonia Rendering Notes (Phase 5.2)
- Terminal text is stored and processed in logical order.
- Visual reordering is applied only inside the rendering pipeline before draw.
- Every output line is right-aligned using measured visual text width, so Latin paths and Arabic text share the same right edge.
- Prompt + input are rendered through the same pipeline for consistent shaping behavior.
- ANSI escape sequences are parsed at render time and stripped from drawn text.
- ANSI styling is applied per span (foreground/background + decorations) without mutating logical history.

### Avalonia Input Notes (Phase 5.3)
- Prompt input is edited in logical order and rendered through BiDi-aware text layout.
- Caret movement uses visual hit-testing for Arabic/English mixed content.
- `Shift + Arrow` extends selection.
- `Ctrl + A/C/X/V` supports select-all, copy, cut, and paste in prompt input.
- Mouse click places caret and mouse drag extends selection in the prompt line.

### Avalonia Terminal Emulator Notes (Phase 5.4)
- The prompt follows the last output line, as in Windows Terminal, and stays inside the viewport once output overflows.
- Mouse wheel scrolls output history.
- `PageUp` and `PageDown` navigate scrollback by viewport-sized steps.
- Click/drag in output history selects full lines across visible rows.
- `Ctrl + C` copies selected output lines first; if no output selection exists, it copies selected prompt input.
- Copied output is emitted in logical line order so external editors receive stable text.

### Avalonia Typography & Theme Notes (Phase 5 Closure)
- Arabic runs, and the spaces and punctuation between Arabic words, render in the bundled Cairo font (`Cairo.ttf`, SIL Open Font License, see `Cairo-OFL.txt`); Latin text, paths, and code stay in the bundled `CascadiaMono.ttf`.
- Lines share one baseline, so Latin-only lines sit level with lines that mix in Cairo's taller Arabic glyphs.
- Default terminal text is 16 logical pixels with a 26-pixel line height on the Windows Terminal Campbell palette (`#0C0C0C` background, `#CCCCCC` text).
- `Ctrl + +` and `Ctrl + -` zoom the terminal text, `Ctrl + 0` restores the default, and holding `Ctrl` while using the mouse wheel zooms in or out.
- ANSI indexed and truecolor output is mapped through a dedicated terminal palette for readability.

### Avalonia Window & Tabs (Windows Terminal Layout, Mirrored)
- The window draws its own title bar: tabs, `+`, and the `⌄` menu start from the right; minimize, maximize, and close sit on the left, as in Arabic Windows.
- The prompt reads `أربش C:\path> ` from the right edge; the path is isolated left-to-right and the `>` (shown as `<`) points at the command typed after it, Arabic or Latin.
- Copying output removes the invisible direction marks the prompt uses.
- Each tab runs an independent session that starts in the current tab's directory.
- `Ctrl + Shift + T` opens a tab, `Ctrl + Shift + W` closes it (the last tab closes the window), `Ctrl + Shift + N` opens a new window, and `Ctrl + Tab` / `Ctrl + Shift + Tab` switch tabs. Middle-click closes a tab.
- Double-click the title bar to maximize or restore; drag it to move the window.
- Elevated sessions show a shield in the title bar and the `المسؤول: أربش` tab title.

## Available Commands

### 1. الأوامر

يعرض قائمة الأوامر الفعّالة في أربش.

**Syntax:**
```powershell
الأوامر
```

**Example:**
```powershell
ArbSh> الأوامر
أوامر أربش العربية:
  الأوامر — يعرض جميع أوامر أربش العربية ووصف كل أمر.
  اعرض — يعرض ملفات مجلد ومجلداته الفرعية المباشرة.
  اخرج — ينهي جلسة أربش الحالية.
  اطبع — يطبع نصًا أو قيمة في مجرى مخرجات الطرفية.
  انتقل — يغيّر مجلد العمل الحالي، أو يعرضه عند حذف المسار.
  اختبار-مصفوفة — يفحص ربط عدة قيم نصية في معامل مصفوفة للمطورين.
  اختبار-نوع — يفحص تحويل القيم إلى أنواع صريحة للمطورين.
  المسار — يعرض مسار مجلد العمل الحالي.
  تشغيل — يترجم ملف باء ويشغّله، أو يشغّل مشروع تكوين الحالي، مع حفظ رمز الخروج والتشخيصات.
  مساعدة — يعرض دليل الأوامر أو شرح أمر عربي محدد.

للتفاصيل: مساعدة <اسم-الأمر>
```

### 2. مساعدة

يعرض مساعدة عامة أو تفاصيل أمر محدد.

**Syntax:**
```powershell
مساعدة [[-الأمر] <string>] [-كامل]
```

**Parameters:**
- `-الأمر <string>` (Position 0): اسم الأمر المطلوب عرض مساعدته
- `-كامل`: عرض تفاصيل المعاملات

**Examples:**

**General Help:**
```powershell
ArbSh> مساعدة
استخدام المساعدة:
  مساعدة <الأمر>
مثال:
  مساعدة الأوامر
```

**Help for Specific Command:**
```powershell
ArbSh> مساعدة الأوامر

الاسم
  الأوامر

الصيغة
  الأوامر
```

**Full Help with Parameters:**
```powershell
ArbSh> مساعدة اطبع -كامل

الاسم
  اطبع

الصيغة
  اطبع [-النص <Object>]

المعاملات
  -النص <Object>
    النص أو الكائن المراد طباعته.
    إلزامي: لا
    الموضع: 0
    من الدفق: نعم (بالقيمة)
```

### 3. اطبع

يطبع نصًا أو كائنًا إلى مجرى المخرجات، ويدعم الإدخال من الدفق.

**Syntax:**
```powershell
اطبع [-النص <Object>]
<PipelineInput> | اطبع
```

**Examples:**

**Direct Output:**
```powershell
ArbSh> اطبع "أهلًا بك في أربش"
أهلًا بك في أربش
```

**Pipeline Usage:**
```powershell
ArbSh> الأوامر | اطبع
انتقل
اعرض
اختبار-مصفوفة
اختبار-نوع
اخرج
اطبع
المسار
الأوامر
مساعدة
```

### 4. انتقل

يغيّر المجلد الحالي للجلسة (session).

**Syntax:**
```powershell
انتقل [[-المسار] <string>]
```

**Examples:**
```powershell
ArbSh> انتقل مشروع
ArbSh> المسار
C:\Work\Project\مشروع
```

### 5. المسار

يعرض المجلد الحالي للجلسة.

**Syntax:**
```powershell
المسار
```

### 6. اعرض

يعرض الملفات والمجلدات في المجلد الحالي أو مسار محدد.

**Syntax:**
```powershell
اعرض [[-المسار] <string>] [-مخفي]
```

**Examples:**
```powershell
ArbSh> اعرض
src/
README.md
```

```powershell
ArbSh> اعرض -مخفي
src/
.gitignore
README.md
```

### 7. تشغيل

يشغّل ملف باء منفردًا أو مشروع تكوين عبر واجهاتهما العامة، ويعيد رمز خروج
البرنامج أو تكوين كما هو.

**Syntax:**
```powershell
تشغيل <ملف.baa|ملف.باء> [وسائط البرنامج...]
تشغيل [مجلد المشروع] [وسائط تكوين...]
```

- ملف باء: يُترجم بـ`baa ملف.baa -o <مجلد مؤقت>` ثم يُشغَّل من مجلد العمل الحالي
  بباقي الوسائط، ويُحذف المجلد المؤقت بعد انتهائه. إذا فشلت الترجمة لا يُشغَّل
  شيء ويبقى رمز خروج `baa` ورسائله.
- بلا ملف: يبحث عن أقرب `مشروع.تكوين` في المجلد الحالي أو ما فوقه، ويشغّل
  `تكوين تشغيل [وسائط]` من جذر المشروع. يمكن تمرير مجلد المشروع صراحة.
- إذا تعذر تحديد ما يُشغَّل تظهر رسالة عربية ورمز الخروج `2`.
- تُبث المخرجات والتشخيصات سطرًا بسطر، ويمر إدخال خط الأنابيب إلى البرنامج.
- يجب أن يكون `baa` و`تكوين` على PATH.

**Examples:**
```powershell
ArbSh> تشغيل مرحبا.baa
مرحباً من باء

ArbSh> انتقل مشروعي/المصدر
ArbSh> تشغيل
مثال Takween MVP
```

### 8. اخرج (Host Command)

أمر مضيف (ليس Cmdlet) لإنهاء جلسة أربش الحالية.

**Syntax:**
```powershell
اخرج
```

## ✅ **NEW: Subexpression Execution `$(...)` - FULLY WORKING**

PowerShell-style command substitution that executes commands and captures their output for use in other commands.

**Syntax:**
```powershell
$(command)
$(command | pipeline)
```

**Examples:**

**Basic Subexpression:**
```powershell
ArbSh> اطبع $(الأوامر)
انتقل
اعرض
اختبار-مصفوفة
اختبار-نوع
اخرج
اطبع
المسار
الأوامر
مساعدة
```

**Subexpression in Parameter:**
```powershell
ArbSh> مساعدة $(اطبع الأوامر)

الاسم
    الأوامر

الصيغة
    الأوامر
```

**Complex Pipeline Subexpression:**
```powershell
ArbSh> اطبع "Available commands: $(الأوامر | اطبع)"
Available commands: انتقل
اعرض
اختبار-مصفوفة
اختبار-نوع
اخرج
اطبع
المسار
الأوامر
مساعدة
```

**Features:**
- Full pipeline execution within subexpressions
- Output capture and string conversion
- Nested subexpression support
- Error handling and debugging
- Integration with parameter binding

## ✅ **NEW: Type Literal Utilization `[TypeName]` - FULLY WORKING**

PowerShell-style type casting that allows explicit type specification for parameters.

**Syntax:**
```powershell
[TypeName] value
[int] 42
[string] hello
[bool] true
```

**Supported Types:**
- `[int]` → Int32
- `[string]` → String
- `[bool]` → Boolean
- `[double]` → Double
- `[datetime]` → DateTime
- `[ConsoleColor]` → ConsoleColor enum

**Examples:**

**Basic Type Casting:**
```powershell
ArbSh> اختبار-نوع [int] 42
عدد-صحيح: 42 (النوع: Int32)
نص: '' (النوع: null)
منطقي: False (النوع: Boolean)
عشري: 0 (النوع: Double)
تاريخ: 1/1/0001 12:00:00 AM (النوع: DateTime)
لون: Black (النوع: ConsoleColor)
مصفوفة-أعداد: فارغة أو null
```

**Multiple Type Literals:**
```powershell
ArbSh> اختبار-نوع [int] 1 [string] hello [bool] true
عدد-صحيح: 1 (النوع: Int32)
نص: 'hello' (النوع: String)
منطقي: True (النوع: Boolean)
عشري: 0 (النوع: Double)
تاريخ: 1/1/0001 12:00:00 AM (النوع: DateTime)
لون: Black (النوع: ConsoleColor)
مصفوفة-أعداد: فارغة أو null
```

**Enum Type Conversion:**
```powershell
ArbSh> اختبار-نوع [ConsoleColor] Red
عدد-صحيح: 12 (النوع: Int32)
نص: '' (النوع: null)
منطقي: False (النوع: Boolean)
عشري: 0 (النوع: Double)
تاريخ: 1/1/0001 12:00:00 AM (النوع: DateTime)
لون: Black (النوع: ConsoleColor)
مصفوفة-أعداد: فارغة أو null
```

**DateTime Parsing:**
```powershell
ArbSh> اختبار-نوع [datetime] 2023-01-01
عدد-صحيح: 0 (النوع: Int32)
نص: '' (النوع: null)
منطقي: False (النوع: Boolean)
عشري: 0 (النوع: Double)
تاريخ: 1/1/2023 12:00:00 AM (النوع: DateTime)
لون: Black (النوع: ConsoleColor)
مصفوفة-أعداد: فارغة أو null
```

**Complex Type Literal Usage:**
```powershell
ArbSh> اختبار-نوع [int] 1 [string] hello [bool] true [double] 3.14 [datetime] 2023-01-01
عدد-صحيح: 1 (النوع: Int32)
نص: 'hello' (النوع: String)
منطقي: True (النوع: Boolean)
عشري: 3.14 (النوع: Double)
تاريخ: 1/1/2023 12:00:00 AM (النوع: DateTime)
لون: Black (النوع: ConsoleColor)
مصفوفة-أعداد: فارغة أو null
```

**Features:**
- Automatic type resolution with aliases
- Positional parameter mapping
- Type conversion with fallback mechanisms
- Support for enums and complex types
- Integration with parameter binding system

## Variable Expansion (`$variableName`)

Variables start with `$` followed by their name. The parser replaces variable tokens with stored values before command execution.

**Note:** Variable storage uses predefined test variables. Full session state management is planned for future versions.

**Available Test Variables:**
- `$testVar` → "Value from $testVar!"
- `$pathExample` → "/path/to/example"
- `$emptyVar` → "" (empty string)

**Examples:**

**Simple Variable Expansion:**
```powershell
ArbSh> اطبع $testVar
Value from $testVar!
```

**Variable with Adjacent Text:**
```powershell
ArbSh> اطبع ValueIs:$testVar
ValueIs:Value from $testVar!
```

**Undefined Variable:**
```powershell
ArbSh> اطبع $nonExistentVar
# (Empty output)
```

**Variable in Parameter:**
```powershell
ArbSh> مساعدة -الأمر $testVar
تعذّر العثور على الأمر: Value from $testVar!
```

## Escape Characters (`\`)

The backslash (`\`) escapes special characters and provides literal interpretation.

**Escape Rules:**
- **Outside Quotes:** Treats following character literally (space, `$`, `;`, `|`)
- **Inside Double Quotes:** C-style escape sequences (`\n`, `\t`, `\"`, `\\`, `\$`)
- **Inside Single Quotes:** All characters treated literally (no escaping)

**Examples:**

**Escaping Operators:**
```powershell
ArbSh> اطبع Command1 \| Command2 ; اطبع Argument\;WithSemicolon
Command1 | Command2
Argument;WithSemicolon
```

**Escaping Quotes:**
```powershell
ArbSh> اطبع "Argument with \"escaped quote\""
Argument with "escaped quote"
```

**Escaping Paths:**
```powershell
ArbSh> اطبع "Path: C:\\Users\\Test"
Path: C:\Users\Test
```

**Newlines and Tabs:**
```powershell
ArbSh> اطبع "First Line\nSecond Line\tIndented"
First Line
Second Line	Indented
```

**Escaping Variables:**
```powershell
ArbSh> اطبع "Value is \$testVar"
Value is $testVar

ArbSh> اطبع \$testVar
$testVar
```

**Escaping Spaces:**
```powershell
ArbSh> اطبع Argument\ WithSpace
Argument WithSpace
```

## Pipeline and Redirection

**Pipeline (`|`):** Passes output from one command to the input of the next with task-based concurrency.

**Basic Pipeline:**
```powershell
ArbSh> الأوامر | اطبع
انتقل
اعرض
اختبار-مصفوفة
اختبار-نوع
اخرج
اطبع
المسار
الأوامر
مساعدة
```

**Pipeline with Quoted Pipes:**
```powershell
ArbSh> اطبع "Value is | this" | اطبع
Value is | this
```

**Output Redirection:**
- `>` - Overwrite file with stdout
- `>>` - Append stdout to file

```powershell
ArbSh> الأوامر > commands.txt
ArbSh> اطبع "Additional line" >> commands.txt
```

**Command Separator (`;`):** Execute multiple statements sequentially.

```powershell
ArbSh> اطبع "First"; اطبع "Second"
First
Second
```

**Advanced Redirection:**

**Error Stream Redirection:**
```powershell
# Redirect stderr to file
ArbSh> Some-Command-That-Errors 2> error.log

# Append stderr to file
ArbSh> Another-Command-That-Errors 2>> error.log
```

**Stream Merging:**
```powershell
# Merge stderr to stdout
ArbSh> Command-With-Errors 2>&1 | اطبع

# Merge stdout to stderr
ArbSh> Command-With-Output 1>&2
```

**Combined Redirection:**
```powershell
# Both streams to same file
ArbSh> Command-With-Both > output.log 2>&1

# Separate files for each stream
ArbSh> Command-With-Both > output.log 2> error.log
```

**Input Redirection (`<`):**
```powershell
# Create input file
ArbSh> اطبع "Line 1" > input.txt
ArbSh> اطبع "Line 2" >> input.txt

# Use input redirection
ArbSh> اطبع < input.txt
Line 1
Line 2
```

## Arabic Language Support

ArbSh is designed as an Arabic-first shell with comprehensive Arabic language support.

### Arabic Command Names

Commands are exposed with Arabic-first names for native Arabic developers:

**Available Arabic Commands:**
- `الأوامر` - عرض قائمة الأوامر
- `مساعدة` - عرض المساعدة العامة أو مساعدة أمر محدد
- `اطبع` - كتابة النص/الكائن إلى المخرجات
- `انتقل` - تغيير المجلد الحالي للجلسة
- `اعرض` - عرض الملفات/المجلدات في المسار الحالي أو مسار محدد
- `المسار` - عرض المجلد الحالي للجلسة
- `اختبار-مصفوفة` - اختبار ربط المصفوفات
- `اختبار-نوع` - اختبار تحويلات الأنواع
- `اخرج` - إنهاء الجلسة (أمر مضيف)

**Examples:**

**Arabic Help Command:**
```powershell
ArbSh> مساعدة

استخدام المساعدة:
  مساعدة <الأمر>
مثال:
  مساعدة الأوامر
```

**Arabic Help with Parameters:**
```powershell
ArbSh> مساعدة -الأمر الأوامر

الاسم
  الأوامر

الصيغة
  الأوامر
```

**Mixed Arabic/English Usage:**
```powershell
ArbSh> اطبع "hello"
ArbSh> اطبع "مرحبا hi"
```

### BiDi Text Processing

ArbSh includes complete Unicode BiDi (Bidirectional) text processing according to UAX #9 standards:

**Implemented BiDi Rules:**
- ✅ **P Rules (P2-P3):** Paragraph embedding level determination
- ✅ **X Rules (X1-X10):** Explicit formatting codes (LRE, RLE, PDF, LRO, RLO, LRI, RLI, FSI, PDI)
- ✅ **W Rules (W1-W7):** Weak type resolution (ES, ET, EN, AN handling)
- ✅ **N Rules (N0-N2):** Neutral type resolution and boundary neutrals
- ✅ **I Rules (I1-I2):** Implicit embedding levels for strong types
- ✅ **L Rules (L1-L4):** Level-based reordering and combining marks

**BiDi Testing:**
- 70+ Unicode BidiTest.txt compliance tests passing
- Comprehensive test coverage for all rule sets
- Real-time BiDi processing for mixed Arabic/English content

### Arabic-First Philosophy

ArbSh prioritizes Arabic language support as a core feature:

**Design Principles:**
- Native Arabic command names
- Full Unicode BiDi text rendering compliance
- Arabic developer workflow optimization
- Cultural localization considerations
- Arabic-first documentation and examples

**Phase 5 Arabic UX Delivered:**
- Scrollback virtualization for large output histories
- Clipboard operations spanning output/selectable history regions
- Keyboard and pointer scrollback navigation for long-running sessions
- Packaged Arabic-capable font fallback for predictable rendering across machines
- ANSI color/styling support for modern CLI output streams

## Testing and Development

**Running ArbSh:**
```powershell
# Build everything
dotnet build src_csharp/ArbSh.sln

# Run Console host
dotnet run --project src_csharp/ArbSh.Console

# Run Avalonia terminal host
dotnet run --project src_csharp/ArbSh.Terminal

# Exit the shell
ArbSh> اخرج
```

**Testing Features:**
```powershell
# Test array binding command
ArbSh> اختبار-مصفوفة

# Test type literals
ArbSh> اختبار-نوع [int] 42

# Test file management commands
ArbSh> المسار
ArbSh> اعرض
ArbSh> انتقل ..

# Test subexpressions
ArbSh> اطبع $(الأوامر)

# Test Arabic commands
ArbSh> مساعدة
```

**Build Release + Installer Package (Windows):**
```powershell
# Console release zip
.\create-release.ps1 -Version "0.8.1-alpha"

# Console + installer package zip
.\create-release.ps1 -Version "0.8.1-alpha" -CreateInstaller

# Install from installer package folder
.\Install-ArbSh.ps1
```

This comprehensive feature set makes ArbSh a powerful Arabic-first shell environment for Arabic developers, with full Unicode compliance and modern shell capabilities.
