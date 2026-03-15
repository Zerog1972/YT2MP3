using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WinForms = System.Windows.Forms;

namespace YT2MP3
{
    public partial class MainWindow : Window
    {
        private const string CaptionInfo = "Information";
        private const string CaptionError = "Erreur";
        private const string CaptionResult = "Résultat";

        private static readonly string LogDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YT2MP3",
            "logs");

        private static readonly string LogFilePath = Path.Combine(LogDirectoryPath, "app.log");

        private CancellationTokenSource _cts;

        public MainWindow()
        {
            InitializeComponent();

            var saved = Properties.Settings.Default.OutputFolder;
            TxtOutputFolder.Text = !string.IsNullOrWhiteSpace(saved)
                ? saved
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MP3");

            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            TxtToolsVersion.Text = string.Format("v{0}.{1}.{2}", ver.Major, ver.Minor, ver.Build);

            Loaded += MainWindow_Loaded;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                SetStatus("Annulation...");
                e.Handled = true;
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeToolsAsync();
        }

        private async Task InitializeToolsAsync()
        {
            if (ToolManager.YtDlpReady && ToolManager.FfmpegReady)
            {
                SetStatus("yt-dlp et ffmpeg prêts - Prêt");
                return;
            }

            ToggleUi(false);
            SetStatus("Téléchargement des outils...");

            try
            {
                await ToolManager.EnsureToolsAsync(SetStatus, CancellationToken.None);
                SetStatus("Prêt");
            }
            catch (Exception ex)
            {
                LogError("Initialisation des outils", ex);
                SetStatus("Erreur - outils manquants");
                ShowError(
                    "Le téléchargement des outils a échoué :\n" + ex.Message +
                    "\n\nVérifiez votre connexion internet et relancez l'application.");
            }

            BtnConvert.IsEnabled = ToolManager.YtDlpReady;
            TxtUrls.IsEnabled = true;
            TxtOutputFolder.IsEnabled = true;
            BtnBrowse.IsEnabled = true;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Sélectionnez le dossier de sortie";
                dialog.SelectedPath = TxtOutputFolder.Text;
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    TxtOutputFolder.Text = dialog.SelectedPath;
                    SaveOutputFolder(dialog.SelectedPath);
                }
            }
        }

        private async void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            var urls = ParseUrls(TxtUrls.Text);
            if (urls.Count == 0)
            {
                ShowInfo("Veuillez saisir au moins une URL YouTube valide.");
                return;
            }

            var outputFolder = TxtOutputFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                ShowInfo("Veuillez indiquer un dossier de sortie.");
                return;
            }

            if (!Path.IsPathRooted(outputFolder))
            {
                ShowInfo("Le dossier de sortie doit être un chemin absolu.");
                return;
            }

            try
            {
                Directory.CreateDirectory(outputFolder);
            }
            catch (Exception ex)
            {
                LogError("Création du dossier de sortie", ex);
                ShowError("Impossible d'utiliser le dossier de sortie : " + ex.Message);
                return;
            }

            _cts = new CancellationTokenSource();
            ToggleUi(false);
            SaveOutputFolder(outputFolder);
            ConversionProgressBar.Minimum = 0;
            ConversionProgressBar.Maximum = urls.Count;
            ConversionProgressBar.Value = 0;
            SetStatus(string.Format("0/{0} - Conversion en cours...", urls.Count));

            var failed = 0;

            try
            {
                for (var i = 0; i < urls.Count; i++)
                {
                    if (_cts.Token.IsCancellationRequested)
                    {
                        SetStatus("Conversion annulée.");
                        break;
                    }

                    var url = urls[i];
                    SetStatus(string.Format("{0}/{1} - Conversion en cours...", i + 1, urls.Count));

                    var args = BuildYtDlpArguments(url, outputFolder);
                    ProcessResult result;
                    try
                    {
                        result = await RunProcessAsync(ToolManager.YtDlpPath, args, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        SetStatus("Conversion annulée.");
                        break;
                    }

                    if (result.ExitCode == 0)
                    {
                        RemoveUrlFromTextBox(url);
                    }
                    else
                    {
                        failed++;
                        LogMessage("Échec conversion", "URL=" + url + Environment.NewLine + result.Output);
                    }

                    ConversionProgressBar.Value = i + 1;
                }

                if (!_cts.Token.IsCancellationRequested)
                {
                    var summary = failed == 0
                        ? string.Format("OK - {0} fichier(s) convertis avec succès.", urls.Count)
                        : string.Format("Terminé - {0} succès, {1} échec(s).", urls.Count - failed, failed);
                    SetStatus(summary);

                    var msg = failed == 0
                        ? "Toutes les conversions sont terminées avec succès."
                        : string.Format("{0} conversion(s) ont échoué.", failed);
                    MessageBox.Show(this, msg, CaptionResult, MessageBoxButton.OK,
                        failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogError("Conversion", ex);
                SetStatus("Erreur inattendue : " + ex.Message);
                ShowError("Impossible d'exécuter la conversion.\nVérifiez que yt-dlp et ffmpeg sont disponibles.");
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                ToggleUi(true);
                ConversionProgressBar.Value = 0;
            }
        }

        private void ShowInfo(string message)
        {
            MessageBox.Show(this, message, CaptionInfo, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, CaptionError, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void SetStatus(string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetStatus(text));
                return;
            }

            TxtStatus.Text = text;
        }

        private void ToggleUi(bool enabled)
        {
            TxtUrls.IsEnabled = enabled;
            TxtOutputFolder.IsEnabled = enabled;
            BtnBrowse.IsEnabled = enabled;
            BtnConvert.IsEnabled = enabled;
        }

        private void RemoveUrlFromTextBox(string url)
        {
            var lines = TxtUrls.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.Equals(l.Trim(), url.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            TxtUrls.Text = string.Join(Environment.NewLine, lines);
        }

        private static void SaveOutputFolder(string path)
        {
            Properties.Settings.Default.OutputFolder = path;
            Properties.Settings.Default.Save();
        }

        private static List<string> ParseUrls(string raw)
        {
            return (raw ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(IsYouTubeUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsYouTubeUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                return false;

            var host = uri.Host.ToLowerInvariant();
            return host.Contains("youtube.com") || host.Contains("youtu.be");
        }

        private static string BuildYtDlpArguments(string url, string outputFolder)
        {
            var outputTemplate = Path.Combine(outputFolder, "%(title)s.%(ext)s");
            var ffmpegArg = ToolManager.FfmpegReady
                ? string.Format("--ffmpeg-location \"{0}\" ", ToolManager.ToolsDir)
                : string.Empty;
            return string.Format("{0}--no-playlist -x --audio-format mp3 --audio-quality 0 -o \"{1}\" \"{2}\"",
                ffmpegArg, outputTemplate, url);
        }

        private static Task<ProcessResult> RunProcessAsync(string fileName, string arguments, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<ProcessResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = new StringBuilder();

            if (token.IsCancellationRequested)
            {
                tcs.TrySetCanceled();
                return tcs.Task;
            }

            var process = new Process();
            CancellationTokenRegistration cancellationRegistration = default(CancellationTokenRegistration);

            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += (s, ev) =>
            {
                if (!string.IsNullOrWhiteSpace(ev.Data)) output.AppendLine(ev.Data);
            };
            process.ErrorDataReceived += (s, ev) =>
            {
                if (!string.IsNullOrWhiteSpace(ev.Data)) output.AppendLine(ev.Data);
            };

            process.Exited += (s, ev) =>
            {
                cancellationRegistration.Dispose();
                tcs.TrySetResult(new ProcessResult { ExitCode = process.ExitCode, Output = output.ToString() });
                process.Dispose();
            };

            cancellationRegistration = token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
                tcs.TrySetCanceled();
            });

            if (!process.Start())
            {
                cancellationRegistration.Dispose();
                process.Dispose();
                throw new InvalidOperationException("Le processus de conversion n'a pas pu démarrer.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }

        private static void LogError(string context, Exception ex)
        {
            LogMessage(context, ex.ToString());
        }

        private static void LogMessage(string context, string details)
        {
            try
            {
                Directory.CreateDirectory(LogDirectoryPath);
                var entry = string.Format(
                    "[{0:yyyy-MM-dd HH:mm:ss}] {1}{2}{3}{2}{2}",
                    DateTime.Now,
                    context,
                    Environment.NewLine,
                    details ?? string.Empty);
                File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
            }
            catch
            {
                // Ne jamais bloquer l'application pour un problème de log.
            }
        }

        private sealed class ProcessResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
        }
    }
}