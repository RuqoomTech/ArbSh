using System.ComponentModel;
using System.Runtime.CompilerServices;
using ArbSh.Terminal.Rendering;
using ArbSh.Terminal.ViewModels;

namespace ArbSh.Terminal;

/// <summary>
/// علامة تبويب في النافذة: جلسة صدفة مستقلة مع سطح الرسم الخاص بها.
/// </summary>
public sealed class TerminalTab : INotifyPropertyChanged
{
    private bool _isSelected;
    private double _width = 240;

    /// <summary>
    /// ينشئ علامة تبويب لجلسة جديدة، ويبقي سطحها حيًا بين التبديلات كي لا يضيع السجل أو الإدخال.
    /// </summary>
    public TerminalTab(MainWindowViewModel session)
    {
        Session = session;
        Surface = new TerminalSurface { DataContext = session };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel Session { get; }

    public TerminalSurface Surface { get; }

    public string Title => Session.Title;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public double Width
    {
        get => _width;
        set => SetField(ref _width, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
