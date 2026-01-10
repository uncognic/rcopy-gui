using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;

namespace rcopy_gui
{
    public enum CopyMode
    {
        RegularCopy = 0,
        Backup = 1
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Robocopy _runner = new();
        private readonly IDialogService _dialogService;
        private CancellationTokenSource? _cts;

        private const int MinThreads = 1;
        private const int MaxThreads = 128;

        private int _threads = Environment.ProcessorCount;
        private string _source = string.Empty;
        private string _destination = string.Empty;
        private string _options = string.Empty;
        private string _log = string.Empty;
        private bool _isRunning;
        private int _progress;
        private CopyMode _mode = CopyMode.RegularCopy;

        public string Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }
        public string Destination
        {
            get => _destination;
            set => SetProperty(ref _destination, value);
        }
        public int Threads
        {
            get => _threads;
            set
            {
                int clamped = Math.Clamp(value, MinThreads, MaxThreads);
                if (SetProperty(ref _threads, clamped))
                {
                    UpdateOptionsForMode();
                }
            }
        }

        public CopyMode Mode
        {
            get => _mode;
            set
            {
                if (SetProperty(ref _mode, value))
                {
                    UpdateOptionsForMode();
                }
            }
        }

        public string Options
        {
            get => _options;
            set => SetProperty(ref _options, value);
        }
        public string Log {
            get => _log;
            private set => SetProperty(ref _log, value);
        }
        public bool IsRunning 
        { get => _isRunning;
            private set { SetProperty(ref _isRunning, value);
                StartCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            } 
        }
        public int Progress {
            get => _progress; 
            private set => SetProperty(ref _progress, value);
        }

        public RelayCommand StartCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand BrowseSourceCommand { get; }
        public RelayCommand BrowseDestinationCommand { get; }
        public MainViewModel()
            : this(new DialogService())
        {
        }
        public MainViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            StartCommand = new RelayCommand(() => _ = StartAsync(), () => !IsRunning);
            CancelCommand = new RelayCommand(Cancel, () => IsRunning);

            BrowseSourceCommand = new RelayCommand(() => _ = BrowseSourceAsync());
            BrowseDestinationCommand = new RelayCommand(() => _ = BrowseDestinationAsync());

            UpdateOptionsForMode();

            _runner.LineReceived += line => System.Windows.Application.Current.Dispatcher.Invoke(() => AppendLog(line));
            _runner.ProgressChanged += p => System.Windows.Application.Current.Dispatcher.Invoke(() => Progress = p);
        }

        private void UpdateOptionsForMode()
        {
            string baseOptions;
            if (Mode == CopyMode.Backup)
            {
                baseOptions = "/E /XO /XJ /COPY:DATSO /DCOPY:T /ZB /R:3 /W:2 /V /NP";
            }
            else
            {
                baseOptions = "/E /XJ /COPY:DAT /DCOPY:T /Z /R:3 /W:2 /V /NP";
            }
                
            Options = $"{baseOptions} /MT:{Threads}";
        }

        private void AppendLog(string line)
        {
            var sb = new StringBuilder(Log);
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(line);
            Log = sb.ToString();
        }

        private async Task BrowseSourceAsync()
        {
            var picked = await _dialogService.PickFolderAsync(Source, "Select source folder").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(picked))
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => Source = picked);
            }
        }

        private async Task BrowseDestinationAsync()
        {
            var picked = await _dialogService.PickFolderAsync(Destination, "Select destination folder").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(picked))
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => Destination = picked);
            }
        }

        public async Task StartAsync()
        {
            if (IsRunning) return;

            if (string.IsNullOrWhiteSpace(Source) || string.IsNullOrWhiteSpace(Destination))
            {
                AppendLog("Source and Destination must be set.");
                return;
            }

            IsRunning = true;
            Progress = 0;
            Log = string.Empty;
            _cts = new CancellationTokenSource();

            try
            {
                string commandLine = Options;

                AppendLog($"robocopy \"{Source}\" \"{Destination}\" {commandLine}");
                int exitCode = await _runner.RunAsync(Source, Destination, commandLine, _cts.Token).ConfigureAwait(false);

                System.Windows.Application.Current.Dispatcher.Invoke(() => AppendLog($"Robocopy exited with code {exitCode}."));
            }
            catch (OperationCanceledException)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => AppendLog("Operation canceled."));
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => AppendLog($"Error: {ex.Message}"));
            }
            finally
            {
                IsRunning = false;
                _cts.Dispose();
                _cts = null;
            }
        }

        private void Cancel()
        {
            _cts?.Cancel();
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value!;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
        #endregion
    }
}