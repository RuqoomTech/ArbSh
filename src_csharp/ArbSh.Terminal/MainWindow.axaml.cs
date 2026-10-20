using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ArbSh.Terminal.ViewModels;

namespace ArbSh.Terminal;

public partial class MainWindow : Window
{
    private const double MaximumTabWidth = 240;
    private const double MinimumTabWidth = 90;

    // عرض ما يجاور علامات التبويب في الشريط: الدرع، و«+» و«⌄»، وأزرار النافذة، وأقل مساحة للسحب.
    private const double ReservedTitleBarWidth = 34 + 44 + 32 + (3 * 46) + 48;

    private readonly ObservableCollection<TerminalTab> _tabs = [];
    private readonly ItemsControl _tabStrip;
    private readonly Panel _surfaceHost;
    private readonly Control _maximizeGlyph;
    private readonly Control _restoreGlyph;
    private readonly Control _titleBar;
    private TerminalTab? _selectedTab;

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(string? initialWorkingDirectory)
    {
        InitializeComponent();

        _tabStrip = this.FindControl<ItemsControl>("TabStrip")!;
        _surfaceHost = this.FindControl<Panel>("SurfaceHost")!;
        _maximizeGlyph = this.FindControl<Control>("MaximizeGlyph")!;
        _restoreGlyph = this.FindControl<Control>("RestoreGlyph")!;
        _tabStrip.ItemsSource = _tabs;

        bool elevated = MainWindowViewModel.IsElevated;
        this.FindControl<Control>("ShieldIcon")!.IsVisible = elevated;
        this.FindControl<Control>("ShieldSpacer")!.IsVisible = !elevated;
        _titleBar = this.FindControl<Control>("TitleBar")!;
        _titleBar.SizeChanged += (_, _) => UpdateTabWidths();

        // اختصارات Windows Terminal تسبق معالجة السطح للمفاتيح.
        AddHandler(KeyDownEvent, HandleShortcutKeyDown, RoutingStrategies.Tunnel);

        AddTab(initialWorkingDirectory);
    }

    /// <summary>
    /// علامات التبويب المفتوحة بالترتيب.
    /// </summary>
    public IReadOnlyList<TerminalTab> Tabs => _tabs;

    /// <summary>
    /// علامة التبويب الظاهرة.
    /// </summary>
    public TerminalTab? SelectedTab => _selectedTab;

    /// <summary>
    /// يفتح علامة تبويب جديدة بجلسة مستقلة ويعرضها.
    /// </summary>
    public TerminalTab AddTab(string? workingDirectory)
    {
        var session = new MainWindowViewModel(workingDirectory);
        var tab = new TerminalTab(session);
        session.ExitRequested += (_, _) => CloseTab(tab);

        _tabs.Add(tab);
        _surfaceHost.Children.Add(tab.Surface);
        SelectTab(tab);
        UpdateTabWidths();
        return tab;
    }

    /// <summary>
    /// يغلق علامة تبويب؛ وتُغلق النافذة مع آخرها كما في Windows Terminal.
    /// </summary>
    public void CloseTab(TerminalTab tab)
    {
        int index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        _tabs.RemoveAt(index);
        _surfaceHost.Children.Remove(tab.Surface);
        tab.Surface.DataContext = null;

        if (_tabs.Count == 0)
        {
            _selectedTab = null;
            Close();
            return;
        }

        if (ReferenceEquals(tab, _selectedTab))
        {
            SelectTab(_tabs[Math.Min(index, _tabs.Count - 1)]);
        }

        UpdateTabWidths();
    }

    /// <summary>
    /// يعرض علامة التبويب ويعيد التركيز إلى طرفيتها.
    /// </summary>
    public void SelectTab(TerminalTab tab)
    {
        foreach (TerminalTab candidate in _tabs)
        {
            bool selected = ReferenceEquals(candidate, tab);
            candidate.IsSelected = selected;
            candidate.Surface.IsVisible = selected;
        }

        _selectedTab = tab;
        Title = tab.Title;
        Dispatcher.UIThread.Post(() => tab.Surface.Focus());
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            bool maximized = WindowState == WindowState.Maximized;
            _maximizeGlyph.IsVisible = !maximized;
            _restoreGlyph.IsVisible = maximized;
        }
    }

    private void UpdateTabWidths()
    {
        if (_tabs.Count == 0)
        {
            return;
        }

        double barWidth = _titleBar.Bounds.Width > 0 ? _titleBar.Bounds.Width : Width;
        double available = Math.Max(0, barWidth - ReservedTitleBarWidth);
        double width = Math.Clamp(Math.Floor(available / _tabs.Count), MinimumTabWidth, MaximumTabWidth);
        foreach (TerminalTab tab in _tabs)
        {
            tab.Width = width;
        }
    }

    private void SelectAdjacentTab(int step)
    {
        if (_selectedTab is null || _tabs.Count < 2)
        {
            return;
        }

        int index = _tabs.IndexOf(_selectedTab);
        SelectTab(_tabs[(index + step + _tabs.Count) % _tabs.Count]);
    }

    private void OpenNewWindow()
    {
        new MainWindow(_selectedTab?.Session.CurrentDirectory).Show();
    }

    private void HandleShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (!ctrl)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.T when shift:
                AddTab(_selectedTab?.Session.CurrentDirectory);
                break;
            case Key.W when shift:
                if (_selectedTab is not null)
                {
                    CloseTab(_selectedTab);
                }

                break;
            case Key.N when shift:
                OpenNewWindow();
                break;
            case Key.Tab:
                SelectAdjacentTab(shift ? -1 : 1);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximized();
        }
        else
        {
            BeginMoveDrag(e);
        }

        e.Handled = true;
    }

    private void Tab_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TerminalTab tab })
        {
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed)
        {
            CloseTab(tab);
        }
        else if (properties.IsLeftButtonPressed)
        {
            SelectTab(tab);
        }

        e.Handled = true;
    }

    private void CloseTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TerminalTab tab })
        {
            CloseTab(tab);
        }
    }

    private void NewTabButton_Click(object? sender, RoutedEventArgs e)
    {
        AddTab(_selectedTab?.Session.CurrentDirectory);
    }

    private void NewWindowMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        OpenNewWindow();
    }

    private void CloseCurrentTabMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedTab is not null)
        {
            CloseTab(_selectedTab);
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        ToggleMaximized();
    }

    private void CloseWindowButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximized()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
