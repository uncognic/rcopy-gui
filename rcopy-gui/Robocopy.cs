using System;
using System.Diagnostics;
using System.IO;
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
            Task stdoutTask = Task.CompletedTask;
            Task stderrTask = Task.CompletedTask;

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

                stdoutTask = Task.Run(async () =>
                {
                    try
                    {
                        using var reader = process.StandardOutput;
                        var buffer = new char[1024];
                        while (true)
                        {
                            int read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                            if (read <= 0) break;
                            var chunk = new string(buffer, 0, read);
                            LineReceived?.Invoke(chunk);
                            TryParseProgress(chunk);
                        }
                    }
                    catch
                    {

                    }
                });

                stderrTask = Task.Run(async () =>
                {
                    try
                    {
                        using var reader = process.StandardError;
                        var buffer = new char[1024];
                        while (true)
                        {
                            int read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                            if (read <= 0) break;
                            var chunk = new string(buffer, 0, read);
                            LineReceived?.Invoke(chunk);
                            TryParseProgress(chunk);
                        }
                    }
                    catch
                    {
                    }
                });

                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    tcs.TrySetResult(process.ExitCode);
                };

                int exitCode = await tcs.Task.ConfigureAwait(false);
                try
                {
                    await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                }
                catch
                {

                }

                return exitCode;
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