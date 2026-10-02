using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

using PinnyNotes.Core.DataTransferObjects;
using PinnyNotes.Core.Enums;
using PinnyNotes.Core.Repositories;
using PinnyNotes.WpfUi.Commands;
using PinnyNotes.WpfUi.Messages;
using PinnyNotes.WpfUi.Models;
using PinnyNotes.WpfUi.Services;
using PinnyNotes.WpfUi.Themes;

namespace PinnyNotes.WpfUi.ViewModels;

public class ManagementViewModel : BaseViewModel, INotifyPropertyChanged
{
    private readonly NoteRepository _noteRepository;
    private readonly ThemeService _themeService;

    private readonly Dictionary<int, NotePreviewModel> _notePreviewsById = [];
    private readonly Dictionary<int, NotePreviewModel> _selectedNotePreviews = [];

    public ManagementViewModel(
        NoteRepository noteRepository,
        AppMetadataService appMetadataService,
        SettingsService settingsService,
        MessengerService messengerService,
        ThemeService themeService
    ) : base(appMetadataService, settingsService, messengerService)
    {
        _noteRepository = noteRepository;
        _themeService = themeService;

        NotePreviewsView = CollectionViewSource.GetDefaultView(NotePreviews);
        NotePreviewsView.Filter = FilterNotePreview;
        NotePreviewsView.SortDescriptions.Add(new SortDescription(nameof(NotePreviewModel.ModifiedAt), ListSortDirection.Descending));
        if (NotePreviewsView is ICollectionViewLiveShaping liveView)
        {
            liveView.IsLiveSorting = true;
            liveView.LiveSortingProperties.Add(nameof(NotePreviewModel.ModifiedAt));
            liveView.IsLiveFiltering = true;
            liveView.LiveFilteringProperties.Add(nameof(NotePreviewModel.Content));
        }

        LoadNotes();

        NewNoteCommand = new RelayCommand(OnNewNoteCommand);
        OpenSettingsCommand = new RelayCommand(OnOpenSettingsCommand);

        OpenNotesCommand = new RelayCommand(OnOpenNotesCommand);
        CloseNotesCommand = new RelayCommand(OnCloseNotesCommand);
        DeleteNotesCommand = new RelayCommand(OnDeleteNotesCommand);

        OpenNoteCommand = new RelayCommand<int>(id => OpenNotes([id]));
        CloseNoteCommand = new RelayCommand<int>(id => CloseNotes([id]));
        DeleteNoteCommand = new RelayCommand<int>(id => DeleteNotes([id]));

        MessengerService.Subscribe<NoteActionMessage>(OnNoteActionMessage);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand NewNoteCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public ICommand OpenNotesCommand { get; }
    public ICommand CloseNotesCommand { get; }
    public ICommand DeleteNotesCommand { get; }

    public ICommand OpenNoteCommand { get; }
    public ICommand CloseNoteCommand { get; }
    public ICommand DeleteNoteCommand { get; }

    public ObservableCollection<NotePreviewModel> NotePreviews { get; } = [];
    public ICollectionView NotePreviewsView { get; }

    public string SearchText
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
            NotePreviewsView.Refresh();
        }
    } = "";

    public void Cleanup()
    {
        MessengerService.Unsubscribe<NoteActionMessage>(OnNoteActionMessage);
        ClearNotePreviews();
    }

    private bool FilterNotePreview(object item)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        return item is NotePreviewModel notePreview
            && notePreview.Content.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase);
    }

    private async void LoadNotes()
    {
        ColourMode colourMode = SettingsService.NoteSettings.ColourMode;

        ClearNotePreviews();

        IEnumerable<NoteDto> noteDtos = await _noteRepository.GetAll();
        foreach (NoteDto noteDto in noteDtos)
            AddNotePreview(noteDto, colourMode);
    }

    private void OnNoteActionMessage(NoteActionMessage message)
    {
        switch (message.Action)
        {
            case NoteAction.Created:
                AddNotePreview(message.NoteDto);
                break;
            case NoteAction.Updated:
                UpdatedNotePreview(message.NoteDto);
                break;
            case NoteAction.Deleted:
                RemoveNotePreview(message.NoteDto.Id);
                break;
        }
    }

    private void UpdatedNotePreview(NoteDto dto)
    {
        if (!_notePreviewsById.TryGetValue(dto.Id, out NotePreviewModel? notePreview))
        {
            AddNotePreview(dto);
            return;
        }

        notePreview.Update(dto);

        UpdateNotePreviewBrushes(notePreview);
    }

    private void UpdateNotePreviewBrushes(NotePreviewModel notePreview, ColourMode? colourMode = null)
    {
        Palette palette = _themeService.GetPalette(
            notePreview.ThemeColourScheme,
            colourMode ?? SettingsService.NoteSettings.ColourMode
        );

        notePreview.UpdateBrushes(palette);
    }

    private void OnNewNoteCommand()
    {
        MessengerService.Publish(new OpenNoteWindowMessage(isManagementWindowParent: true));
    }

    private void OnOpenSettingsCommand()
    {
        MessengerService.Publish(new OpenSettingsWindowMessage());
    }

    private List<int> GetSelectedOrAllNoteIds()
    {
        List<int> noteIds = [.._selectedNotePreviews.Keys]; // Selected
        if (noteIds.Count == 0)
            noteIds = [.._notePreviewsById.Keys]; // All

        return noteIds;
    }

    private void OnOpenNotesCommand()
        => OpenNotes(GetSelectedOrAllNoteIds());

    private void OnCloseNotesCommand()
        => CloseNotes(GetSelectedOrAllNoteIds());

    private void OnDeleteNotesCommand()
    {
        List<int> selectedNoteIds = [.._selectedNotePreviews.Keys];
        if (selectedNoteIds.Count == 0)
            return; // No delete all

        DeleteNotes(selectedNoteIds);
    }

    private void OpenNotes(List<int> noteIds)
    {
        MessengerService.Publish(new MultipleNoteWindowActionMessage(noteIds, NoteWindowAction.Open));
    }

    private void CloseNotes(List<int> noteIds)
    {
        MessengerService.Publish(new MultipleNoteWindowActionMessage(noteIds, NoteWindowAction.Close));
    }

    private async void DeleteNotes(List<int> noteIds)
    {
        string prompt = (noteIds.Count == 1)
            ? "Delete this note? This can't be undone."
            : $"Delete {noteIds.Count} notes? This can't be undone.";
        MessageBoxResult result = MessageBox.Show(prompt, "Delete note", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        CloseNotes(noteIds);

        foreach (int noteId in noteIds)
        {
            if (!_notePreviewsById.ContainsKey(noteId))
                continue; // Note may have been deleted due to being empty when closed above

            RemoveNotePreview(noteId);
            await _noteRepository.Delete(noteId);
        }
    }

    private void NotePreview_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not NotePreviewModel notePreview)
            return;

        switch (e.PropertyName)
        {
            case nameof(NotePreviewModel.IsSelected):

                if (notePreview.IsSelected && !_selectedNotePreviews.ContainsKey(notePreview.Id))
                    _selectedNotePreviews[notePreview.Id] = notePreview;
                else if (!notePreview.IsSelected && _selectedNotePreviews.ContainsKey(notePreview.Id))
                    _selectedNotePreviews.Remove(notePreview.Id);

                break;
        }
    }

    private void ClearNotePreviews()
    {
        foreach (NotePreviewModel notePreview in NotePreviews)
            notePreview.PropertyChanged -= NotePreview_PropertyChanged;

        NotePreviews.Clear();
        _notePreviewsById.Clear();
        _selectedNotePreviews.Clear();
    }

    private void AddNotePreview(NoteDto dto, ColourMode? colourMode = null)
    {
        if (_notePreviewsById.ContainsKey(dto.Id))
            return;

        NotePreviewModel notePreview = new(dto);

        UpdateNotePreviewBrushes(notePreview, colourMode);

        notePreview.PropertyChanged += NotePreview_PropertyChanged;

        NotePreviews.Add(notePreview);
        _notePreviewsById[dto.Id] = notePreview;
        if (notePreview.IsSelected)
            _selectedNotePreviews[notePreview.Id] = notePreview;
    }

    private void RemoveNotePreview(int noteId)
    {
        if (!_notePreviewsById.Remove(noteId, out NotePreviewModel? notePreview))
            return;

        notePreview.PropertyChanged -= NotePreview_PropertyChanged;

        NotePreviews.Remove(notePreview);
        _selectedNotePreviews.Remove(notePreview.Id);
    }
}
