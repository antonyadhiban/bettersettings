using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BetterSettings.App.Models;
using Microsoft.UI.Dispatching;

namespace BetterSettings.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly HybridSearchService _searchService;
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _debounceTimer;

    private string _query = string.Empty;
    private SearchResult? _selectedResult;
    private string _statusMessage = string.Empty;

    public MainViewModel(HybridSearchService searchService)
    {
        _searchService = searchService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        Results = new ObservableCollection<SearchResult>();
        _debounceTimer = _dispatcher.CreateTimer();
        _debounceTimer.Interval = TimeSpan.FromMilliseconds(50); // Fast debounce
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            UpdateResults();
        };

        UpdateResults();
    }

    public ObservableCollection<SearchResult> Results { get; }

    public string Query
    {
        get => _query;
        set
        {
            if (_query != value)
            {
                _query = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasQuery));
                if (!string.IsNullOrWhiteSpace(_statusMessage))
                {
                    StatusMessage = string.Empty;
                }
                RestartDebounce();
            }
        }
    }

    public bool HasQuery => !string.IsNullOrWhiteSpace(_query);

    public SearchResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (_selectedResult != value)
            {
                _selectedResult = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public void SelectNext()
    {
        if (Results.Count == 0)
        {
            return;
        }

        var index = SelectedResult == null ? -1 : Results.IndexOf(SelectedResult);
        if (index < Results.Count - 1)
        {
            SelectedResult = Results[index + 1];
        }
    }

    public void SelectPrevious()
    {
        if (Results.Count == 0)
        {
            return;
        }

        var index = SelectedResult == null ? Results.Count : Results.IndexOf(SelectedResult);
        if (index > 0)
        {
            SelectedResult = Results[index - 1];
        }
    }

    public void ClearQuery()
    {
        Query = string.Empty;
        StatusMessage = string.Empty;
    }

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void UpdateResults()
    {
        var matches = _searchService.Search(Query, 8);
        Results.Clear();
        foreach (var match in matches)
        {
            Results.Add(match);
        }

        SelectedResult = Results.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
