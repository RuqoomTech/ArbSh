using ArbSh.Core;
using ArbSh.Core.Hosting;
using ArbSh.Terminal.Models;
using ArbSh.Terminal.ViewModels;
using Xunit;

namespace ArbSh.Test;

public class ShellInputTests
{
    [Theory]
    [InlineData("المسار\nاعرض", "المسار; اعرض")]
    [InlineData("اعرض |\nالأوامر", "اعرض | الأوامر")]
    [InlineData("اعرض # تعليق\nالمسار", "اعرض ; المسار")]
    [InlineData("اكتب 'سطر\nآخر'", "اكتب 'سطر\nآخر'")]
    [InlineData("\nالمسار\r\n\n", " المسار;  ")]
    public void ToSingleLine_SplitsStatementsOnNewlinesOutsideQuotes(string input, string expected)
    {
        Assert.Equal(expected, Parser.ToSingleLine(input));
    }

    [Fact]
    public void Parse_TreatsEachLineAsAStatement()
    {
        Assert.Equal(2, Parser.Parse("المسار\nاعرض").Count);
        Assert.Single(Parser.Parse("اعرض |\nالأوامر"));
    }

    [Theory]
    [InlineData("اكتب 'نص", true)]
    [InlineData("اكتب \"نص", true)]
    [InlineData("اعرض |", true)]
    [InlineData("اعرض | # ثم", true)]
    [InlineData("اختبار $(المسار", true)]
    [InlineData("اكتب 'نص'", false)]
    [InlineData("اكتب \"a\\\"b\"", false)]
    [InlineData("اكتب '|'", false)]
    [InlineData("اعرض # '", false)]
    public void IsIncomplete_DetectsOpenQuotesBracketsAndTrailingPipe(string input, bool expected)
    {
        Assert.Equal(expected, Parser.IsIncomplete(input));
    }

    [Theory]
    [InlineData("اعرض &", true, "اعرض")]
    [InlineData("اعرض | الأوامر  &  ", true, "اعرض | الأوامر")]
    [InlineData("اطبع 'أ &'", false, "")]
    [InlineData("اطبع 'أ' # تعليق &", false, "")]
    [InlineData("&", false, "")]
    [InlineData("اعرض", false, "")]
    public void TrySplitBackground_RecognizesOnlyATrailingAmpersandOutsideQuotes(string input, bool expected, string command)
    {
        Assert.Equal(expected, Parser.TrySplitBackground(input, out string actual));
        if (expected)
        {
            Assert.Equal(command, actual);
        }
    }

    [Fact]
    public void History_NavigatesBackAndForwardAndRestoresTheDraft()
    {
        var history = new CommandHistory();
        history.Add("أول");
        history.Add("ثان");
        history.Add("ثان");
        history.Add("  ");

        Assert.Equal(["أول", "ثان"], history.Entries);
        Assert.Equal("ثان", history.Previous("مسودة"));
        Assert.Equal("أول", history.Previous("تجاهل"));
        Assert.Null(history.Previous(string.Empty));
        Assert.Equal("ثان", history.Next());
        Assert.Equal("مسودة", history.Next());
        Assert.Null(history.Next());
    }

    [Fact]
    public void History_PersistsAcrossSessionsAndKeepsTheNewestEntries()
    {
        string file = Path.Combine(Path.GetTempPath(), $"أربش سجل {Guid.NewGuid():N}", "history");
        try
        {
            var first = new CommandHistory(file, capacity: 2);
            first.Add("قديم");
            first.Add("اكتب 'سطر\nآخر'");
            first.Add("انتقل 'مجلد عربي'");
            File.AppendAllText(file, "ليس JSON\n");

            var second = new CommandHistory(file, capacity: 2);

            Assert.Equal(["اكتب 'سطر\nآخر'", "انتقل 'مجلد عربي'"], second.Entries);
            Assert.Contains("مجلد عربي", File.ReadAllText(file), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
        }
    }

    [Fact]
    public void Completion_CompletesArabicCommandNamesOnlyAtCommandPosition()
    {
        string root = CreateTempDirectory();
        try
        {
            CompletionResult command = TabCompletion.Complete("المسار; تشغ", 11, root);
            Assert.Equal("المسار; تشغيل ", command.Text);
            Assert.Equal(command.Text.Length, command.Caret);

            CompletionResult argument = TabCompletion.Complete("مساعدة تشغ", 10, root);
            Assert.Equal("مساعدة تشغ", argument.Text);
            Assert.Empty(argument.Candidates);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Completion_CompletesPathsQuotingSpacesAndKeepingDirectoriesOpen()
    {
        string root = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "مجلد عربي"));
            File.WriteAllText(Path.Combine(root, "مجلد عربي", "برنامج.baa"), string.Empty);
            File.WriteAllText(Path.Combine(root, "ملف١.txt"), string.Empty);
            File.WriteAllText(Path.Combine(root, "ملف٢.txt"), string.Empty);
            char separator = Path.DirectorySeparatorChar;

            CompletionResult directory = TabCompletion.Complete("انتقل مجل", 9, root);
            Assert.Equal($"انتقل 'مجلد عربي{separator}", directory.Text);

            CompletionResult file = TabCompletion.Complete(directory.Text + "بر", directory.Text.Length + 2, root);
            Assert.Equal($"انتقل 'مجلد عربي{separator}برنامج.baa' ", file.Text);

            CompletionResult common = TabCompletion.Complete("اعرض مل x", 7, root);
            Assert.Equal("اعرض ملف x", common.Text);
            Assert.Equal(8, common.Caret);
            Assert.Equal(["ملف١.txt", "ملف٢.txt"], common.Candidates);

            CompletionResult ambiguous = TabCompletion.Complete("اعرض ملف", 8, root);
            Assert.Equal("اعرض ملف", ambiguous.Text);
            Assert.Equal(["ملف١.txt", "ملف٢.txt"], ambiguous.Candidates);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ViewModel_JoinsContinuedLinesIntoOneCommandAndRecordsIt()
    {
        string root = CreateTempDirectory();
        string child = Path.Combine(root, "فرعي");
        Directory.CreateDirectory(child);
        try
        {
            var viewModel = new MainWindowViewModel(root);

            await viewModel.SubmitInputAsync($"انتقل '{child}'", continueInput: true);
            Assert.Equal(MainWindowViewModel.ContinuationPrompt, viewModel.Prompt);
            Assert.NotEqual(Path.GetFullPath(child), viewModel.CurrentDirectory);

            await viewModel.SubmitInputAsync(string.Empty);
            Assert.Equal(Path.GetFullPath(child), viewModel.CurrentDirectory);
            Assert.NotEqual(MainWindowViewModel.ContinuationPrompt, viewModel.Prompt);
            Assert.Equal([$"انتقل '{child}'"], viewModel.History.Entries);
            Assert.Equal(2, viewModel.Lines.Count(line => line.Kind == TerminalLineKind.Input));

            await viewModel.SubmitInputAsync("الأوامر |");
            Assert.Equal(MainWindowViewModel.ContinuationPrompt, viewModel.Prompt);
            Assert.True(viewModel.CancelExecution());
            Assert.NotEqual(MainWindowViewModel.ContinuationPrompt, viewModel.Prompt);
            Assert.Single(viewModel.History.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"أربش إدخال {Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
