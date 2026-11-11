using Avalonia.Threading;
using ArbSh.Terminal.Models;
using ArbSh.Terminal.ViewModels;

namespace ArbSh.Test;

public sealed class TerminalInteractiveRunTests
{
    [Fact]
    public async Task TypedLineReachesTheRunningBaaProgramAndCancelEndsAWaitingOne()
    {
        // يحتاج baa على PATH وطرفية Windows الزائفة؛ تفعّله مهمة CI البيئية.
        if (Environment.GetEnvironmentVariable("ARBSH_ECO_INTEGRATION") != "1" || !OperatingSystem.IsWindows())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), $"أربش تفاعل {Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(
                Path.Combine(root, "سؤال.baa"),
                "صحيح الرئيسية() {\n    صحيح س = ٠.\n    اطبع \"أدخل رقماً\".\n    اقرأ س.\n    اطبع س + ١.\n    إرجع ٠.\n}\n");
            var viewModel = new MainWindowViewModel(root);

            Task run = viewModel.SubmitInputAsync("تشغيل سؤال.baa");
            await PumpUntilAsync(viewModel, lines => lines.Any(line => line.Text.Contains("أدخل رقماً", StringComparison.Ordinal)));
            // أثناء عمل البرنامج يذهب السطر إلى إدخاله ولا يُنفذ كأمر صدفة.
            await viewModel.SubmitInputAsync("41");
            await PumpUntilAsync(viewModel, lines => lines.Any(line => line.Text == "42"));
            await run.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal("جاهز", viewModel.StatusText);
            Assert.Single(viewModel.Lines, line => line.Kind == TerminalLineKind.Input);
            Assert.False(viewModel.CancelExecution());

            Task waiting = viewModel.SubmitInputAsync("تشغيل سؤال.baa");
            await PumpUntilAsync(
                viewModel,
                lines => lines.Count(line => line.Text.Contains("أدخل رقماً", StringComparison.Ordinal)) == 2);
            Assert.True(viewModel.CancelExecution());
            await waiting.WaitAsync(TimeSpan.FromSeconds(30));
            await PumpUntilAsync(
                viewModel,
                lines => lines.Any(line => line.Kind == TerminalLineKind.Error && line.Text.Contains("أُلغي", StringComparison.Ordinal)));

            Assert.Equal("جاهز", viewModel.StatusText);
            Assert.Equal(1, viewModel.Lines.Count(line => line.Text == "42"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // خطوط الخرج تُرحّل إلى خيط الواجهة؛ بلا تطبيق Avalonia تُصرف يدويًا.
    private static async Task PumpUntilAsync(
        MainWindowViewModel viewModel,
        Func<IReadOnlyList<TerminalLine>, bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            TerminalLine[] lines = [.. viewModel.Lines];
            if (condition(lines))
            {
                return;
            }

            Assert.True(
                DateTime.UtcNow < deadline,
                "لم يتحقق الشرط. الخطوط:\n" + string.Join("\n", lines.Select(line => $"{line.Kind}: {line.Text}")));
            await Task.Delay(50);
        }
    }
}
