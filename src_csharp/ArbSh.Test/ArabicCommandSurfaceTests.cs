using System.Reflection;
using ArbSh.Core;
using ArbSh.Core.Models;

namespace ArbSh.Test;

public sealed class ArabicCommandSurfaceTests
{
    [Theory]
    [InlineData("مساعدة")]
    [InlineData("الأوامر")]
    [InlineData("اطبع")]
    [InlineData("اختبار-مصفوفة")]
    [InlineData("اختبار-نوع")]
    [InlineData("انتقل")]
    [InlineData("المسار")]
    [InlineData("اعرض")]
    [InlineData("تشغيل")]
    public void Find_ResolvesArabicCommandNames(string commandName)
    {
        Type? cmdletType = CommandDiscovery.Find(commandName);
        Assert.NotNull(cmdletType);
    }

    [Theory]
    [InlineData("Get-Help")]
    [InlineData("Get-Command")]
    [InlineData("Write-Output")]
    [InlineData("احصل-مساعدة")]
    [InlineData("Test-Array-Binding")]
    [InlineData("Test-Type-Literal")]
    public void Find_DoesNotResolveLegacyCommandNames(string commandName)
    {
        Type? cmdletType = CommandDiscovery.Find(commandName);
        Assert.Null(cmdletType);
    }

    [Fact]
    public void GetAllCommands_ContainsOnlyActiveArabicSurface()
    {
        IReadOnlyDictionary<string, Type> commands = CommandDiscovery.GetAllCommands();

        Assert.Contains("مساعدة", commands.Keys);
        Assert.Contains("الأوامر", commands.Keys);
        Assert.Contains("اطبع", commands.Keys);
        Assert.Contains("اختبار-مصفوفة", commands.Keys);
        Assert.Contains("اختبار-نوع", commands.Keys);
        Assert.Contains("انتقل", commands.Keys);
        Assert.Contains("المسار", commands.Keys);
        Assert.Contains("اعرض", commands.Keys);
        Assert.Contains("تشغيل", commands.Keys);
        Assert.DoesNotContain("Get-Help", commands.Keys);
        Assert.DoesNotContain("Get-Command", commands.Keys);
        Assert.DoesNotContain("Write-Output", commands.Keys);
        Assert.DoesNotContain("احصل-مساعدة", commands.Keys);
        Assert.DoesNotContain("Test-Array-Binding", commands.Keys);
        Assert.DoesNotContain("Test-Type-Literal", commands.Keys);
    }

    [Fact]
    public void EveryCmdletAndParameter_HasArabicPublicMetadata()
    {
        Type[] cmdletTypes = typeof(CmdletBase).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(CmdletBase)) && !type.IsAbstract)
            .ToArray();

        Assert.NotEmpty(cmdletTypes);
        foreach (Type cmdletType in cmdletTypes)
        {
            ArabicNameAttribute? commandName = cmdletType.GetCustomAttribute<ArabicNameAttribute>();
            ArabicDescriptionAttribute? description = cmdletType.GetCustomAttribute<ArabicDescriptionAttribute>();

            Assert.NotNull(commandName);
            Assert.NotNull(description);
            Assert.DoesNotMatch("[A-Za-z]", commandName!.Name);
            Assert.DoesNotMatch("[A-Za-z]", description!.Description);

            foreach (PropertyInfo parameter in cmdletType.GetProperties()
                .Where(property => property.GetCustomAttribute<ParameterAttribute>() is not null))
            {
                ArabicNameAttribute? parameterName = parameter.GetCustomAttribute<ArabicNameAttribute>();
                Assert.NotNull(parameterName);
                Assert.DoesNotMatch("[A-Za-z]", parameterName!.Name);
            }
        }
    }

    [Fact]
    public void GetArabicCatalog_ListsEveryCommandInArabicWithDescriptions()
    {
        IReadOnlyList<ArabicCommandInfo> catalog = CommandDiscovery.GetArabicCatalog();

        Assert.Equal(CommandDiscovery.GetAllCommands().Count + 1, catalog.Count);
        Assert.Contains(catalog, command => command.Name == "اخرج");
        foreach (ArabicCommandInfo command in catalog)
        {
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
            Assert.DoesNotMatch("[A-Za-z]", command.Name);
            Assert.DoesNotMatch("[A-Za-z]", command.Description);
        }
    }

    [Fact]
    public void HelpWithoutArguments_ListsEveryArabicCommand()
    {
        var sink = new CaptureSink();
        var session = new ShellSessionState();

        ShellEngine.ExecuteInput("مساعدة", sink, session: session);

        string help = string.Join(Environment.NewLine, sink.Outputs);
        foreach (ArabicCommandInfo command in CommandDiscovery.GetArabicCatalog())
        {
            Assert.Contains(command.Name, help, StringComparison.Ordinal);
            Assert.Contains(command.Description, help, StringComparison.Ordinal);
        }

        Assert.Empty(sink.Errors);
        Assert.Equal(0, session.LastExitCode);
    }

    [Theory]
    [InlineData("مساعدة")]
    [InlineData("الأوامر")]
    [InlineData("اطبع")]
    [InlineData("اختبار-مصفوفة")]
    [InlineData("اختبار-نوع")]
    [InlineData("انتقل")]
    [InlineData("المسار")]
    [InlineData("اعرض")]
    public void EveryEngineCommand_CompletesSmokeExecution(string commandName)
    {
        string root = Path.Combine(Path.GetTempPath(), $"أربش أوامر {Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sink = new CaptureSink();
        var session = new ShellSessionState(root);

        try
        {
            ShellEngine.ExecuteInput(commandName, sink, session: session);

            Assert.Empty(sink.Errors);
            Assert.Equal(0, session.LastExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CaptureSink : IExecutionSink
    {
        public List<string> Outputs { get; } = [];

        public List<string> Errors { get; } = [];

        public void WriteOutput(string message) => Outputs.Add(message);

        public void WriteError(string message) => Errors.Add(message);

        public void WriteWarning(string message)
        {
        }

        public void WriteDebug(string message)
        {
        }
    }
}
