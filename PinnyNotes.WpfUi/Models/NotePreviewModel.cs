using System.Globalization;
using System.Windows.Media;

using PinnyNotes.Core.DataTransferObjects;
using PinnyNotes.WpfUi.Themes;

namespace PinnyNotes.WpfUi.Models;

public class NotePreviewModel : BaseModel
{
    private const int MaxPreviewLength = 300;

    public NotePreviewModel(NoteDto noteDto)
    {
        Id = noteDto.Id;

        Update(noteDto);
    }

    public int Id { get; set => SetProperty(ref field, value); }

    public string Content
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            string trimmedText = value.Trim();
            int previewLength = Math.Min(trimmedText.Length, MaxPreviewLength);
            ContentPreview = trimmedText[..previewLength];
        }
    } = "";

    public string ContentPreview { get; private set => SetProperty(ref field, value); } = "";

    public string ThemeColourScheme { get; set => SetProperty(ref field, value); } = "";

    public bool IsOpen { get; set => SetProperty(ref field, value); }

    public long ModifiedAt
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(ModifiedDisplay));
        }
    }

    public string ModifiedDisplay
    {
        get
        {
            if (ModifiedAt <= 0)
                return "";

            DateTime modified = DateTimeOffset.FromUnixTimeMilliseconds(ModifiedAt).LocalDateTime;
            DateTime now = DateTime.Now;

            if (modified.Date == now.Date)
                return modified.ToString("t", CultureInfo.CurrentCulture);
            if (modified.Year == now.Year)
                return modified.ToString("d MMM", CultureInfo.CurrentCulture);
            return modified.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }
    }

    public Brush BackgroundBrush { get; set => SetProperty(ref field, value); } = Brushes.LightGray;
    public Brush BorderBrush { get; set => SetProperty(ref field, value); } = Brushes.DarkGray;
    public Brush TitleBrush { get; set => SetProperty(ref field, value); } = Brushes.Gray;
    public Brush TextBrush { get; set => SetProperty(ref field, value); } = Brushes.Black;

    public bool IsSelected { get; set => SetProperty(ref field, value); }

    public void Update(NoteDto noteDto)
    {
        Content = noteDto.Content;
        ThemeColourScheme = noteDto.ThemeColourScheme;
        IsOpen = noteDto.IsOpen;
        ModifiedAt = noteDto.ModifiedAt;
    }

    public void UpdateBrushes(Palette palette)
    {
        BackgroundBrush = new SolidColorBrush(palette.Background);
        BorderBrush = new SolidColorBrush(palette.Border);
        TitleBrush = new SolidColorBrush(palette.Title);
        TextBrush = new SolidColorBrush(palette.Text);
    }
}
