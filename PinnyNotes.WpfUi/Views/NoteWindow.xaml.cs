using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

using PinnyNotes.Core.Enums;
using PinnyNotes.WpfUi.Helpers;
using PinnyNotes.WpfUi.Interop;
using PinnyNotes.WpfUi.Messages;
using PinnyNotes.WpfUi.Models;
using PinnyNotes.WpfUi.Services;
using PinnyNotes.WpfUi.Themes;
using PinnyNotes.WpfUi.ViewModels;

namespace PinnyNotes.WpfUi.Views;

public partial class NoteWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly NoteSettingsModel _noteSettings;
    private readonly MessengerService _messengerService;
    private readonly ThemeService _themeService;

    private readonly NoteViewModel _viewModel;

    #region NoteWindow

    public NoteWindow(SettingsService settingsService, MessengerService messengerService, ThemeService themeService, NoteViewModel viewModel)
    {
        _settingsService = settingsService;
        _noteSettings = settingsService.NoteSettings;
        _messengerService = messengerService;
        _messengerService.Subscribe<WindowActionMessage>(OnWindowActionMessage);
        _themeService = themeService;

        _viewModel = viewModel;

        DataContext = _viewModel;

        InitializeComponent();

        SourceInitialized += Window_SourceInitialized;
        Activated += Window_Activated;
        Closing += Window_Closing;
        Closed += Window_Closed;
        Deactivated += Window_Deactivated;
        MouseDown += NoteWindow_MouseDown;
        MouseEnter += Window_MouseEnter;
        MouseLeave += Window_MouseLeave;
        Loaded += Window_Loaded;
        StateChanged += NoteWindow_StateChanged;

        ContentGrid.SizeChanged += ContentGrid_SizeChanged;

        TitleBarGrid.MouseDown += TitleBar_MouseDown;
        NewButton.Click += NewButton_Click;
        NotesListButton.Click += NotesListButton_Click;
        MoreButton.Click += MoreButton_Click;
        CloseButton.Click += CloseButton_Click;
        TitleBarContextMenu.Closed += TitleBarContextMenu_Closed;

        PopulateTitleBarContextMenu();
    }

    private void PopulateTitleBarContextMenu()
    {
        int insertIndex = TitleBarContextMenu.Items.IndexOf(ThemeMenuSeparator);
        foreach (ColourScheme colourScheme in _themeService.CurrentTheme.ColourSchemes.Values)
        {
            MenuItem menuItem = new()
            {
                Header = colourScheme.Name,
                Command = _viewModel.ChangeThemeColourCommand,
                CommandParameter = colourScheme.Name,
                Icon = colourScheme.Icon
            };

            TitleBarContextMenu.Items.Insert(insertIndex, menuItem);

            insertIndex++;
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        // Layered (transparent) windows can't be rounded by DWM, RootBorder handles those.
        if (!AllowsTransparency)
            DwmApi.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);
    }

    private void ContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Clip content so the title bar and text box follow RootBorder's rounded corners.
        double radius = Math.Max(0, RootBorder.CornerRadius.TopLeft - RootBorder.BorderThickness.Left);
        ContentGrid.Clip = new RectangleGeometry(new Rect(e.NewSize), radius, radius);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.OnWindowLoaded(
            ScreenHelper.GetWindowHandle(this)
        );
    }

    private void NoteWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Check mouse button is pressed as a missed click of a button
        // can cause issues with DragMove().
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        DragMove();

        _viewModel.OnWindowMoved(Left, Top);
    }

    private void NoteWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
            return;

        if (_noteSettings.MinimizeMode == MinimizeMode.Prevent || (_noteSettings.MinimizeMode == MinimizeMode.PreventIfPinned && _viewModel.Note.IsPinned))
            WindowState = WindowState.Normal;
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        ShowTitleBar();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!IsActive)
            HideTitleBar();
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        _viewModel.Note.IsFocused = true;
        _viewModel.UpdateOpacity();
        ShowTitleBar();
    }

    private async void Window_Deactivated(object? sender, EventArgs e)
    {
        if (!_viewModel.Note.IsOpen)
            return;

        _viewModel.Note.IsFocused = false;
        _viewModel.UpdateOpacity();
        HideTitleBar();

        await _viewModel.SaveNote();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = await _viewModel.CloseNote();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _messengerService.Unsubscribe<WindowActionMessage>(OnWindowActionMessage);
    }

    private void OnWindowActionMessage(WindowActionMessage message)
    {
        if (message.Action == WindowAction.Activate)
        {
            WindowState = WindowState.Normal;
            Activate();
        }
    }

    #endregion

    #region TitleBar

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            if (WindowState == WindowState.Normal)
                WindowState = WindowState.Maximized;
            else
                WindowState = WindowState.Normal;
        }
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Publish(
            new OpenNoteWindowMessage(ParentNote: _viewModel.Note)
        );
    }

    private void NotesListButton_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Publish(new OpenManagementWindowMessage());
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        TitleBarContextMenu.PlacementTarget = MoreButton;
        TitleBarContextMenu.Placement = PlacementMode.Bottom;
        TitleBarContextMenu.IsOpen = true;
    }

    private void TitleBarContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        // Restore default placement so right clicking the title bar opens at the mouse again.
        TitleBarContextMenu.ClearValue(ContextMenu.PlacementTargetProperty);
        TitleBarContextMenu.ClearValue(ContextMenu.PlacementProperty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => CloseNote();

    /// <summary>
    /// Closes the window and marks the note as closed, so it isn't restored on next start up.
    /// </summary>
    public void CloseNote()
    {
        _viewModel.Note.IsOpen = false;
        Close();
    }

    private void HideTitleBar()
    {
        if (_noteSettings.HideTitleBar)
            BeginStoryboard("HideTitleBarAnimation");
    }

    private void ShowTitleBar()
    {
        BeginStoryboard("ShowTitleBarAnimation");
    }

    private void BeginStoryboard(string resourceKey)
    {
        Storyboard hideTitleBar = (Storyboard)FindResource(resourceKey);
        hideTitleBar.Begin();
    }

    private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog saveFileDialog = new()
        {
            Filter = "Text Documents (*.txt)|*.txt|All Files|*"
        };

        if (saveFileDialog.ShowDialog(this) == false)
            return;

        File.WriteAllText(saveFileDialog.FileName, NoteTextBox.Text);
    }

    private void ResetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Width = _noteSettings.DefaultWidth;
        Height = _noteSettings.DefaultHeight;
    }

    private void ManagementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Publish(new OpenManagementWindowMessage());
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Publish(new OpenSettingsWindowMessage(this));
    }

    private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDeleteWindow.Confirm(this, _settingsService))
            return;

        await _viewModel.DeleteNote();
        Close();
    }

    #endregion
}
