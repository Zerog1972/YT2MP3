using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Thierry JOUVE - 12 mars 2026

namespace YT2MP3
{
    public partial class FormMain : Form
    {
        private CancellationTokenSource _cts;

        public FormMain()
        {
            InitializeComponent();

            var saved = Properties.Settings.Default.OutputFolder;
            txtOutputFolder.Text = !string.IsNullOrWhiteSpace(saved)
                ? saved
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MP3");

            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            tsslTools.Text = string.Format("v{0}.{1}.{2}", ver.Major, ver.Minor, ver.Build);

            toolTip.SetToolTip(txtUrls,         "Collez une ou plusieurs URLs YouTube, une par ligne.");
            toolTip.SetToolTip(txtOutputFolder, "Dossier où les fichiers MP3 seront enregistrés.");
            toolTip.SetToolTip(btnBrowse,       "Choisir le dossier de sortie.");
            toolTip.SetToolTip(btnConvert,      "Lancer la conversion des URLs en MP3.");

            this.Shown += async (s, e) => await InitializeToolsAsync();

            this.KeyPreview = true;
            this.KeyDown   += Form1_KeyDown;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                SetStatus("Annulation…");
                e.Handled = true;
            }
        }

        // ── Initialisation des outils ──────────────────────────────────────────────

        private async Task InitializeToolsAsync()
        {
            if (ToolManager.YtDlpReady && ToolManager.FfmpegReady)
            {
                SetStatus("yt-dlp et ffmpeg prêts — Prêt");
                return;
            }

            ToggleUi(false);
            SetStatus("Téléchargement des outils…");

            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    await ToolManager.EnsureToolsAsync(msg => SetStatus(msg), cts.Token);
                    SetStatus("Prêt");
                }
                catch (Exception ex)
                {
                    SetStatus("Erreur — outils manquants");
                    MessageBox.Show(this,
                        "Le téléchargement des outils a échoué :\n" + ex.Message +
                        "\n\nVérifiez votre connexion internet et relancez l'application.",
                        "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            btnConvert.Enabled      = ToolManager.YtDlpReady;
            txtUrls.Enabled         = true;
            txtOutputFolder.Enabled = true;
            btnBrowse.Enabled       = true;
        }

        // ── Événements boutons ─────────────────────────────────────────────────────

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description  = "Sélectionnez le dossier de sortie";
                dialog.SelectedPath = txtOutputFolder.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtOutputFolder.Text = dialog.SelectedPath;
                    SaveOutputFolder(dialog.SelectedPath);
                }
            }
        }

        private async void btnConvert_Click(object sender, EventArgs e)
        {
            var urls = ParseUrls(txtUrls.Text);
            if (!urls.Any())
            {
                MessageBox.Show(this, "Veuillez saisir au moins une URL YouTube valide.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var outputFolder = txtOutputFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                MessageBox.Show(this, "Veuillez indiquer un dossier de sortie.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!Path.IsPathRooted(outputFolder))
            {
                MessageBox.Show(this, "Le dossier de sortie doit être un chemin absolu.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try { Directory.CreateDirectory(outputFolder); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Impossible d'utiliser le dossier de sortie : " + ex.Message,
                    "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _cts = new CancellationTokenSource();
            ToggleUi(false);
            SaveOutputFolder(outputFolder);
            progressBar.Minimum = 0;
            progressBar.Maximum = urls.Count;
            progressBar.Value   = 0;
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
                    }

                    progressBar.Value = i + 1;
                }

                if (!_cts.Token.IsCancellationRequested)
                {
                    var summary = failed == 0
                        ? string.Format("✔  {0} fichier(s) convertis avec succès.", urls.Count)
                        : string.Format("⚠  Terminé — {0} succès, {1} échec(s).", urls.Count - failed, failed);
                    SetStatus(summary);

                    var msg = failed == 0
                        ? "Toutes les conversions sont terminées avec succès."
                        : string.Format("{0} conversion(s) ont échoué.", failed);
                    MessageBox.Show(this, msg, "Résultat", MessageBoxButtons.OK,
                        failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Erreur inattendue : " + ex.Message);
                MessageBox.Show(this,
                    "Impossible d'exécuter la conversion.\nVérifiez que yt-dlp et ffmpeg sont disponibles.",
                    "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                ToggleUi(true);
                progressBar.Value = 0;
            }
        }

        // ── Helpers UI ─────────────────────────────────────────────────────────────

        private void SetStatus(string text)
        {
            if (tsslStatus.GetCurrentParent()?.InvokeRequired == true)
            {
                tsslStatus.GetCurrentParent().Invoke(new Action<string>(SetStatus), text);
                return;
            }
            tsslStatus.Text = text;
        }

        private void ToggleUi(bool enabled)
        {
            txtUrls.Enabled         = enabled;
            txtOutputFolder.Enabled = enabled;
            btnBrowse.Enabled       = enabled;
            btnConvert.Enabled      = enabled;
        }

        private void RemoveUrlFromTextBox(string url)
        {
            if (txtUrls.InvokeRequired)
            {
                txtUrls.Invoke(new Action<string>(RemoveUrlFromTextBox), url);
                return;
            }
            var lines = txtUrls.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.Equals(l.Trim(), url.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            txtUrls.Text = string.Join(Environment.NewLine, lines);
        }

        private static void SaveOutputFolder(string path)
        {
            Properties.Settings.Default.OutputFolder = path;
            Properties.Settings.Default.Save();
        }

        // ── Logique métier ─────────────────────────────────────────────────────────

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
            var tcs    = new TaskCompletionSource<ProcessResult>();
            var output = new StringBuilder();

            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName               = fileName,
                Arguments              = arguments,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += (s, ev) => { if (!string.IsNullOrWhiteSpace(ev.Data)) output.AppendLine(ev.Data); };
            process.ErrorDataReceived  += (s, ev) => { if (!string.IsNullOrWhiteSpace(ev.Data)) output.AppendLine(ev.Data); };

            process.Exited += (s, ev) =>
            {
                tcs.TrySetResult(new ProcessResult { ExitCode = process.ExitCode, Output = output.ToString() });
                process.Dispose();
            };

            token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
                tcs.TrySetCanceled();
            });

            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("Le processus de conversion n'a pas pu démarrer.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }

        private sealed class ProcessResult
        {
            public int    ExitCode { get; set; }
            public string Output   { get; set; }
        }
    }
}
