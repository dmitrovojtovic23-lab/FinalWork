using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace DownloaderApp
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<DownloadItem> Downloads { get; set; } = new ObservableCollection<DownloadItem>();
        private SemaphoreSlim _parallelSemaphore;
        private int _maxParallel = 3;
        private readonly string _stateFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DownloaderApp", "state.json");

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            DownloadsGrid.ItemsSource = Downloads;
            LoadState();
            UpdateStats();
        }

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Clipboard.ContainsText()) UrlTextBox.Text = System.Windows.Clipboard.GetText();
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Оберіть будь-який файл у потрібній папці",
                CheckFileExists = true
            };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                string filePath = dialog.FileName;
                string folderPath = System.IO.Path.GetDirectoryName(filePath)!;
                DestinationTextBox.Text = folderPath;
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                System.Windows.MessageBox.Show("Введіть URL.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                System.Windows.MessageBox.Show("Невірний URL.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(DestinationTextBox.Text) || !Directory.Exists(DestinationTextBox.Text))
            {
                System.Windows.MessageBox.Show("Оберіть коректну папку для збереження.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(MaxParallelTextBox.Text, out _maxParallel) || _maxParallel < 1) _maxParallel = 3;
            _parallelSemaphore = new SemaphoreSlim(_maxParallel);

            var tags = TagsTextBox.Text?.Trim() ?? "";
            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(fileName)) fileName = $"download_{DateTime.Now:yyyyMMddHHmmss}";

            var item = new DownloadItem
            {
                Id = Downloads.Count + 1,
                Url = url,
                FileName = fileName,
                Destination = DestinationTextBox.Text,
                Tags = tags,
                Status = DownloadStatus.Waiting
            };

            Downloads.Add(item);
            SaveState();
            UpdateStats();
            _ = StartQueuedDownloadAsync(item); 
            UrlTextBox.Clear();
            TagsTextBox.Clear();
        }

        private async Task StartQueuedDownloadAsync(DownloadItem item)
        {
            if (_parallelSemaphore == null) _parallelSemaphore = new SemaphoreSlim(_maxParallel);
            await _parallelSemaphore.WaitAsync();
            try
            {
                if (item.Status == DownloadStatus.Completed || item.Status == DownloadStatus.Downloading) return;
                item.Status = DownloadStatus.Downloading;
                UpdateStats();
                await item.StartAsync();
                UpdateStats();
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Failed;
                item.ErrorMessage = ex.Message;
                UpdateStats();
            }
            finally
            {
                _parallelSemaphore.Release();
                SaveState();
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!(GetClickedItem(sender) is DownloadItem item)) return;
            if (_parallelSemaphore == null) _parallelSemaphore = new SemaphoreSlim(_maxParallel);
            _ = StartQueuedDownloadAsync(item);
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (!(GetClickedItem(sender) is DownloadItem item)) return;
            item.Pause();
            UpdateStats();
            SaveState();
        }

        private void BtnResume_Click(object sender, RoutedEventArgs e)
        {
            if (!(GetClickedItem(sender) is DownloadItem item)) return;
            if (_parallelSemaphore == null) _parallelSemaphore = new SemaphoreSlim(_maxParallel);
            _ = StartQueuedDownloadAsync(item);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!(GetClickedItem(sender) is DownloadItem item)) return;
            item.Cancel();
            if (File.Exists(item.FullPath))
            {
                var res = System.Windows.MessageBox.Show($"Видалити файл {item.FullPath}?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    try { File.Delete(item.FullPath); } catch { }
                }
            }
            Downloads.Remove(item);
            ReindexIds();
            UpdateStats();
            SaveState();
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (!(GetClickedItem(sender) is DownloadItem item)) return;
            if (File.Exists(item.FullPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() { FileName = item.FullPath, UseShellExecute = true });
            }
            else
            {
                System.Windows.MessageBox.Show("Файл не знайдено.", "Інфо", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            var del = Downloads.Where(d => d.Status == DownloadStatus.Completed || d.Status == DownloadStatus.Failed).ToList();
            foreach (var d in del) Downloads.Remove(d);
            ReindexIds();
            UpdateStats();
            SaveState();
        }

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            var tag = FilterTagTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(tag))
            {
                System.Windows.MessageBox.Show("Введіть тег для фільтра", "Інфо", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var lower = tag.ToLowerInvariant();
            var filtered = Downloads.Where(d => !string.IsNullOrEmpty(d.Tags) && d.Tags.ToLowerInvariant().Split(',').Select(t => t.Trim()).Contains(lower)).ToList();
            DownloadsGrid.ItemsSource = filtered;
        }

        private void BtnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            DownloadsGrid.ItemsSource = Downloads;
        }

        private DownloadItem GetClickedItem(object sender)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is DownloadItem item) return item;
            return null;
        }

        private void ReindexIds()
        {
            int i = 1;
            foreach (var d in Downloads) d.Id = i++;
        }

        private void UpdateStats()
        {
            Dispatcher.Invoke(() =>
            {
                var active = Downloads.Count(d => d.Status == DownloadStatus.Downloading);
                var ok = Downloads.Count(d => d.Status == DownloadStatus.Completed);
                var failed = Downloads.Count(d => d.Status == DownloadStatus.Failed);
                StatsTextBlock.Text = $"Активні / Вдалі / Невдалі: {active} / {ok} / {failed}";
            });
        }

        private void SaveState()
        {
            try
            {
                var dir = Path.GetDirectoryName(_stateFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var save = Downloads.Select(d => d.ToSerializable()).ToList();
                File.WriteAllText(_stateFile, JsonSerializer.Serialize(save, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadState()
        {
            try
            {
                if (!File.Exists(_stateFile)) return;
                var json = File.ReadAllText(_stateFile);
                var arr = JsonSerializer.Deserialize<List<DownloadItem.Serializable>>(json);
                if (arr == null) return;
                foreach (var s in arr)
                {
                    var di = DownloadItem.FromSerializable(s);
                    Downloads.Add(di);
                    if (di.Status == DownloadStatus.Downloading) di.Status = DownloadStatus.Paused;
                }
            }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public enum DownloadStatus { Waiting, Downloading, Paused, Completed, Failed, Cancelled }

    public class DownloadItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public string FileName { get; set; }
        public string Destination { get; set; }
        public string Tags { get; set; }
        public string FullPath => Path.Combine(Destination ?? "", FileName ?? "");
        private double _progress;
        public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(nameof(Progress)); } }

        private DownloadStatus _status;
        public DownloadStatus Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusWithSpeed)); } }

        public string ErrorMessage { get; set; }
        private long _downloadedBytes = 0;
        private long _totalBytes = -1;
        private CancellationTokenSource _cts;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private long _lastUpdateBytes = 0;
        private double _lastSpeed = 0;

        public string StatusWithSpeed
        {
            get
            {
                var status = Status.ToString();
                if (Status == DownloadStatus.Downloading)
                {
                    var totalStr = _totalBytes > 0 ? $"{FormatSize(_downloadedBytes)}/{FormatSize(_totalBytes)}" : $"{FormatSize(_downloadedBytes)}";
                    var sp = _lastSpeed > 0 ? $"{FormatSize((long)_lastSpeed)}/s" : "";
                    return $"{status} — {totalStr} {sp}";
                }
                else if (Status == DownloadStatus.Completed) return "Completed";
                else if (Status == DownloadStatus.Paused) return "Paused";
                else if (Status == DownloadStatus.Failed) return $"Failed: {ErrorMessage}";
                else return status;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        public async Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            Status = DownloadStatus.Downloading;

            using var client = new HttpClient();
            try
            {
                long existingLength = 0;
                if (File.Exists(FullPath))
                {
                    var fi = new FileInfo(FullPath);
                    existingLength = fi.Length;
                }
                try
                {
                    var head = new HttpRequestMessage(HttpMethod.Head, Url);
                    var headResp = await client.SendAsync(head);
                    if (headResp.IsSuccessStatusCode && headResp.Content.Headers.ContentLength.HasValue)
                    {
                        _totalBytes = headResp.Content.Headers.ContentLength.Value;
                    }
                }
                catch { }

                var request = new HttpRequestMessage(HttpMethod.Get, Url);
                if (existingLength > 0) request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);

                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength.HasValue)
                {
                    if (existingLength > 0) _totalBytes = existingLength + response.Content.Headers.ContentLength.Value;
                    else _totalBytes = response.Content.Headers.ContentLength.Value;
                }

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fs = new FileStream(FullPath, FileMode.Append, FileAccess.Write, FileShare.None, 8192, useAsync: true);

                var buffer = new byte[8192];
                int bytesRead;
                _lastUpdateTime = DateTime.UtcNow;
                _lastUpdateBytes = existingLength;
                _downloadedBytes = existingLength;

                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cts.Token)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead), _cts.Token);
                    _downloadedBytes += bytesRead;

                    if (_totalBytes > 0) Progress = Math.Round((_downloadedBytes / (double)_totalBytes) * 100, 2);
                    else Progress = Math.Min(99.9, Progress + bytesRead / 100000.0);

                    var now = DateTime.UtcNow;
                    var elapsed = (now - _lastUpdateTime).TotalSeconds;
                    if (elapsed >= 1)
                    {
                        var delta = _downloadedBytes - _lastUpdateBytes;
                        _lastSpeed = delta / Math.Max(1, elapsed);
                        _lastUpdateTime = now;
                        _lastUpdateBytes = _downloadedBytes;
                        OnPropertyChanged(nameof(StatusWithSpeed));
                    }
                }

                Progress = 100;
                Status = DownloadStatus.Completed;
            }
            catch (OperationCanceledException)
            {
                Status = DownloadStatus.Paused;
            }
            catch (Exception ex)
            {
                Status = DownloadStatus.Failed;
                ErrorMessage = ex.Message;
            }
        }

        public void Pause()
        {
            if (_cts != null && !_cts.IsCancellationRequested) _cts.Cancel();
            Status = DownloadStatus.Paused;
        }

        public void Cancel()
        {
            if (_cts != null && !_cts.IsCancellationRequested) _cts.Cancel();
            Status = DownloadStatus.Cancelled;
        }

        private static string FormatSize(long b)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (b == 0) return "0B";
            var abs = Math.Abs((double)b);
            var place = Convert.ToInt32(Math.Floor(Math.Log(abs, 1024)));
            place = Math.Min(place, suf.Length - 1);
            double num = Math.Round(abs / Math.Pow(1024, place), 2);
            return $"{(Math.Sign(b) * num).ToString()} {suf[place]}";
        }
        public Serializable ToSerializable() => new Serializable
        {
            Id = Id,
            Url = Url,
            FileName = FileName,
            Destination = Destination,
            Tags = Tags,
            Progress = Progress,
            Status = Status,
            ErrorMessage = ErrorMessage,
            DownloadedBytes = _downloadedBytes,
            TotalBytes = _totalBytes
        };

        public static DownloadItem FromSerializable(Serializable s)
        {
            return new DownloadItem
            {
                Id = s.Id,
                Url = s.Url,
                FileName = s.FileName,
                Destination = s.Destination,
                Tags = s.Tags,
                Progress = s.Progress,
                Status = s.Status,
                ErrorMessage = s.ErrorMessage,
                _downloadedBytes = s.DownloadedBytes,
                _totalBytes = s.TotalBytes
            };
        }

        public class Serializable
        {
            public int Id { get; set; }
            public string Url { get; set; }
            public string FileName { get; set; }
            public string Destination { get; set; }
            public string Tags { get; set; }
            public double Progress { get; set; }
            public DownloadStatus Status { get; set; }
            public string ErrorMessage { get; set; }
            public long DownloadedBytes { get; set; }
            public long TotalBytes { get; set; }
        }
    }
}
