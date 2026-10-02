using System.Windows;

using PinnyNotes.WpfUi.Services;

namespace PinnyNotes.WpfUi.Views;

public partial class ConfirmDeleteWindow : Window
{
    private ConfirmDeleteWindow(int noteCount)
    {
        InitializeComponent();

        if (noteCount > 1)
        {
            TitleTextBlock.Text = $"Delete {noteCount} notes?";
            MessageTextBlock.Text = "These notes will be permanently deleted. This can't be undone.";
        }

        DeleteButton.Click += (s, e) => DialogResult = true;
    }

    /// <summary>
    /// Asks the user to confirm deleting notes, unless they have turned confirmation off.
    /// Ticking "Don't ask me again" turns the ConfirmDelete setting off, it can be turned back on in settings.
    /// </summary>
    public static async Task<bool> Confirm(Window? owner, SettingsService settingsService, int noteCount = 1)
    {
        if (!settingsService.NoteSettings.ConfirmDelete)
            return true;

        ConfirmDeleteWindow dialog = new(noteCount);
        if (owner is not null && owner.IsVisible)
            dialog.Owner = owner;
        else
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        if (dialog.ShowDialog() != true)
            return false;

        if (dialog.DontAskAgainCheckBox.IsChecked == true)
        {
            settingsService.NoteSettings.ConfirmDelete = false;
            await settingsService.Save();
        }

        return true;
    }
}
