using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YT2MP3
{
    /// <summary>
    /// Gère yt-dlp.exe et ffmpeg.exe dans le sous-dossier tools\ de l'application.
    /// Télécharge automatiquement les binaires si absents.
    /// </summary>
    internal static class ToolManager
    {
        public static readonly string ToolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
        public static readonly string YtDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yt-dlp.exe");
        public static readonly string FfmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg.exe");

        private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private const string FfmpegUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        public static bool YtDlpReady => File.Exists(YtDlpPath);
        public static bool FfmpegReady => File.Exists(FfmpegPath);

        public static async Task EnsureToolsAsync(Action<string> onProgress, CancellationToken token)
        {
            Directory.CreateDirectory(ToolsDir);

            if (!YtDlpReady)
                await DownloadYtDlpAsync(onProgress, token);
            else
                onProgress("yt-dlp déjà présent.");

            if (!FfmpegReady)
                await DownloadFfmpegAsync(onProgress, token);
            else
                onProgress("ffmpeg déjà présent.");
        }

        private static async Task DownloadYtDlpAsync(Action<string> onProgress, CancellationToken token)
        {
            onProgress("Téléchargement de yt-dlp...");
            var tmp = YtDlpPath + ".tmp";
            try
            {
                await DownloadFileAsync(YtDlpUrl, tmp,
                    pct => onProgress(string.Format("yt-dlp {0,3}%", pct)), token);
                File.Move(tmp, YtDlpPath);
                onProgress("yt-dlp téléchargé.");
            }
            catch
            {
                if (File.Exists(tmp)) File.Delete(tmp);
                throw;
            }
        }

        private static async Task DownloadFfmpegAsync(Action<string> onProgress, CancellationToken token)
        {
            onProgress("Téléchargement de ffmpeg...");
            var zipPath = Path.Combine(ToolsDir, "ffmpeg.zip");
            try
            {
                await DownloadFileAsync(FfmpegUrl, zipPath,
                    pct => onProgress(string.Format("ffmpeg {0,3}%", pct)), token);

                onProgress("Extraction de ffmpeg...");
                await Task.Run(() => ZipExtractSingleFile(zipPath, "ffmpeg.exe", FfmpegPath), token);
                onProgress("ffmpeg extrait.");
            }
            catch
            {
                if (File.Exists(FfmpegPath)) File.Delete(FfmpegPath);
                throw;
            }
            finally
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }

        /// <summary>
        /// Extrait le premier fichier dont le nom de fichier correspond à <paramref name="entryName"/>
        /// en parsant le format ZIP binaire directement (Store et Deflate).
        /// Aucune dépendance externe requise.
        /// </summary>
        private static void ZipExtractSingleFile(string zipPath, string entryName, string destPath)
        {
            const uint SigLocal = 0x04034B50;
            const uint SigCentral = 0x02014B50;

            using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs, Encoding.UTF8))
            {
                while (fs.Position + 4 <= fs.Length)
                {
                    var sig = br.ReadUInt32();

                    if (sig == SigCentral)
                        break;

                    if (sig != SigLocal)
                    {
                        // Data descriptor ou signature inconnue : on cherche le prochain header.
                        SkipToNextSignature(fs, br);
                        continue;
                    }

                    // Local file header
                    br.ReadUInt16(); // version needed
                    var flags = br.ReadUInt16();
                    var compression = br.ReadUInt16(); // 0 = Store, 8 = Deflate
                    br.ReadUInt32(); // mod time + date
                    br.ReadUInt32(); // crc32
                    var compSize = (long)br.ReadUInt32();
                    var uncompSize = (long)br.ReadUInt32();
                    var nameLen = br.ReadUInt16();
                    var extraLen = br.ReadUInt16();

                    var rawName = br.ReadBytes(nameLen);
                    br.ReadBytes(extraLen);

                    var fullName = Encoding.UTF8.GetString(rawName).Replace('\\', '/');
                    var slash = fullName.LastIndexOf('/');
                    var fileName = slash >= 0 ? fullName.Substring(slash + 1) : fullName;

                    bool isTarget = string.Equals(fileName, entryName, StringComparison.OrdinalIgnoreCase);

                    if (isTarget && compression == 0)
                    {
                        ExtractStore(fs, uncompSize, destPath);
                        return;
                    }

                    if (isTarget && compression == 8)
                    {
                        ExtractDeflate(fs, compSize, destPath);
                        return;
                    }

                    if (isTarget)
                        throw new NotSupportedException(
                            string.Format("Méthode de compression {0} non supportée.", compression));

                    // Sauter les données de cette entrée
                    if ((flags & 8) != 0)
                        SkipToNextSignature(fs, br);
                    else
                        fs.Seek(compSize, SeekOrigin.Current);
                }
            }

            throw new FileNotFoundException(
                string.Format("'{0}' introuvable dans l'archive ZIP.", entryName));
        }

        private static void ExtractStore(Stream src, long size, string destPath)
        {
            var buf = new byte[81920];
            using (var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                long remaining = size;
                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(buf.Length, remaining);
                    var read = src.Read(buf, 0, toRead);
                    if (read == 0) break;
                    dst.Write(buf, 0, read);
                    remaining -= read;
                }
            }
        }

        private static void ExtractDeflate(Stream src, long compSize, string destPath)
        {
            var compressed = new byte[compSize];
            var offset = 0;
            while (offset < compSize)
            {
                var read = src.Read(compressed, offset, (int)(compSize - offset));
                if (read == 0) break;
                offset += read;
            }

            using (var compStream = new MemoryStream(compressed))
            using (var deflate = new System.IO.Compression.DeflateStream(
                       compStream, System.IO.Compression.CompressionMode.Decompress))
            using (var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                deflate.CopyTo(dst);
            }
        }

        /// <summary>Avance jusqu'à la prochaine signature de header local (PK\x03\x04) ou central (PK\x01\x02).</summary>
        private static void SkipToNextSignature(Stream fs, BinaryReader br)
        {
            while (fs.Position + 4 <= fs.Length)
            {
                if (br.ReadByte() != 0x50) continue;
                if (fs.Position + 3 > fs.Length) break;
                var b2 = br.ReadByte();
                if (b2 != 0x4B) continue;
                var b3 = br.ReadByte();
                var b4 = br.ReadByte();
                if ((b3 == 0x03 && b4 == 0x04) || (b3 == 0x01 && b4 == 0x02))
                {
                    fs.Seek(-4, SeekOrigin.Current);
                    return;
                }
            }
        }

        private static async Task DownloadFileAsync(string url, string destPath,
            Action<int> onPercent, CancellationToken token)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "YT2MP3-App";
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream())
            using (var file = new FileStream(destPath, FileMode.Create, FileAccess.Write,
                                             FileShare.None, 81920, useAsync: true))
            {
                var total = response.ContentLength;
                var buffer = new byte[81920];
                long downloaded = 0;
                var lastPct = -1;
                int read;

                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                    downloaded += read;
                    if (total > 0)
                    {
                        var pct = (int)(downloaded * 100L / total);
                        if (pct != lastPct)
                        {
                            lastPct = pct;
                            onPercent(pct);
                        }
                    }
                }
            }
        }
    }
}
