using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace rcopy_gui
{
    public class Robocopy
    {
        private static readonly Regex PercentRegex = new(@"(\d{1,3})%", RegexOptions.Compiled);

        public event Action<string>? LineReceived;
        public event Action<int>? ProgressChanged;

        public async Task<int> RunAsync(string source, string destination, string options, CancellationToken cancellationToken)
        {
            var args = $"{Quote(source)} {Quote(destination)} {options}".Trim();

            using var process = new Process();
            var psi = new ProcessStartInfo
            {
                FileName = "robocopy",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.Default,
                StandardErrorEncoding = System.Text.Encoding.Default,
            };
            process.StartInfo = psi;

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data is null) return;
                LineReceived?.Invoke(e.Data);
                TryParseProgress(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data is null) return;
                LineReceived?.Invoke(e.Data);
                TryParseProgress(e.Data);
            };

            cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { }
            });

            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("Failed to start robocopy process.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    tcs.TrySetResult(process.ExitCode);
                };

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                try { process.CancelOutputRead(); } catch { }
                try { process.CancelErrorRead(); } catch { }
            }
        }

        private void TryParseProgress(string line)
        {
            var m = PercentRegex.Match(line);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var val))
            {
                var bounded = Math.Max(0, Math.Min(100, val));
                ProgressChanged?.Invoke(bounded);
            }
        }

        private static string Quote(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            if (s.StartsWith("\"") && s.EndsWith("\"")) return s;
            return $"\"{s}\"";
        }
    }
}