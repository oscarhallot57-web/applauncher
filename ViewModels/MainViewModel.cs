using System.Collections.ObjectModel;
using System.Windows.Input;
using AppLauncher.Helpers;
using AppLauncher.Models;
using AppLauncher.Services;

namespace AppLauncher.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly ApplicationScanner _scanner = new();
        private readonly ApplicationSearchService _searchService = new();
        private readonly ApplicationLauncher _launcher = new();
        private readonly CacheService _cacheService = new();
        private readonly IconService _iconService = new();

        private List<ApplicationItem> _allApps = new();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value)) UpdateResults();
            }
        }

        public ObservableCollection<ApplicationItem> Results { get; } = new();

        private ApplicationItem? _selectedItem;
        public ApplicationItem? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => SetProperty(ref _isEmpty, value);
        }

        /// <summary>Raised when the user presses Escape.</summary>
        public event Action? RequestClose;
        /// <summary>Raised right after an application was successfully launched.</summary>
        public event Action? RequestLaunchAndClose;

        public ICommand LaunchSelectedCommand { get; }
        public ICommand MoveSelectionDownCommand { get; }
        public ICommand MoveSelectionUpCommand { get; }
        public ICommand CloseCommand { get; }

        public MainViewModel()
        {
            LaunchSelectedCommand = new RelayCommand(_ => LaunchSelected());
            MoveSelectionDownCommand = new RelayCommand(_ => MoveSelection(1));
            MoveSelectionUpCommand = new RelayCommand(_ => MoveSelection(-1));
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        /// <summary>Loads the cache instantly, then confirms/updates it with a fresh scan in the background.</summary>
        public async Task InitializeAsync()
        {
            List<ApplicationItem>? cached = null;
            try
            {
                cached = await _cacheService.LoadAsync();
                if (cached is { Count: > 0 })
                {
                    _allApps = cached;
                    UpdateResults();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "MainViewModel.InitializeAsync (cache load)");
            }

            try
            {
                var fresh = await _scanner.ScanAsync();

                if (cached != null)
                {
                    var cachedByPath = cached.ToDictionary(a => a.SourceFilePath, StringComparer.OrdinalIgnoreCase);
                    foreach (var app in fresh)
                    {
                        if (cachedByPath.TryGetValue(app.SourceFilePath, out var old))
                        {
                            app.LaunchCount = old.LaunchCount;
                            app.LastLaunchedUtc = old.LastLaunchedUtc;
                        }
                    }
                }

                _allApps = fresh;
                UpdateResults();

                await _cacheService.SaveAsync(_allApps);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "MainViewModel.InitializeAsync (scan)");
            }
        }

        public void ResetSearch() => SearchText = string.Empty;

        private void UpdateResults()
        {
            var matches = _searchService.Search(_allApps, SearchText);

            Results.Clear();
            foreach (var m in matches) Results.Add(m);

            IsEmpty = Results.Count == 0 && !string.IsNullOrWhiteSpace(SearchText);
            SelectedItem = Results.FirstOrDefault();

            _ = LoadIconsAsync(matches);
        }

        private async Task LoadIconsAsync(List<ApplicationItem> items)
        {
            foreach (var item in items)
            {
                if (item.Icon != null) continue;
                try
                {
                    item.Icon = await _iconService.GetIconAsync(item);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Icon load failed for '{item.ExecutablePath}'");
                }
            }
        }

        private void MoveSelection(int delta)
        {
            if (Results.Count == 0) return;

            int currentIndex = SelectedItem != null ? Results.IndexOf(SelectedItem) : -1;
            int newIndex = Math.Clamp(currentIndex + delta, 0, Results.Count - 1);
            SelectedItem = Results[newIndex];
        }

        private void LaunchSelected()
        {
            if (SelectedItem == null) return;

            if (_launcher.Launch(SelectedItem))
            {
                RequestLaunchAndClose?.Invoke();
            }
        }

        public void Launch(ApplicationItem item)
        {
            SelectedItem = item;
            LaunchSelected();
        }
    }
}
