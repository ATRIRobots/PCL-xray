using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Acceleration;

public sealed class XrayService : IDisposable
{
    private Process? _proxyProcess;
    private string _workingDir = null!;
    private string _knifePath = null!;
    private string _corePath = null!;
    private System.Timers.Timer? _autoUpdateTimer;
    private bool _autoUpdateEnabled;

    public int ListenPort { get; private set; } = 1080;
    public bool IsRunning => _proxyProcess is { HasExited: false };
    public event Action<string>? LogReceived;

    private const string BuiltinRemark = "AutoMergePublicNodes";

    public void Initialize(string workingDir, string knifePath, string corePath)
    {
        _workingDir = workingDir;
        _knifePath = knifePath;
        _corePath = corePath;
        Directory.CreateDirectory(_workingDir);
    }

    // ─────────────────────────── 订阅管理 ───────────────────────────

    /// <summary>确保内置节点池订阅已存在（幂等）。</summary>
    public async Task EnsureBuiltinSubscriptionAsync(string builtinUrl)
    {
        var subs = await ListSubscriptionsAsync();
        // 旧框架没有 string.Contains(string, StringComparison)，改用 IndexOf
        if (subs.Any(s => s.IndexOf(BuiltinRemark, StringComparison.OrdinalIgnoreCase) >= 0))
            return;
        await RunKnifeAsync($"subs add --url \"{builtinUrl}\" --remark \"{BuiltinRemark}\"");
    }

    /// <summary>导入外部订阅，自动去重。</summary>
    public async Task<bool> ImportExternalSubscriptionAsync(string url, string? remark = null)
    {
        var name = string.IsNullOrWhiteSpace(remark)
            ? $"User_{DateTime.Now:yyyyMMddHHmmss}"
            : remark.Trim();
        var subs = await ListSubscriptionsAsync();
        if (subs.Any(s => s.IndexOf(url, StringComparison.OrdinalIgnoreCase) >= 0))
            return false;
        await RunKnifeAsync($"subs add --url \"{url}\" --remark \"{name}\"");
        return true;
    }

    /// <summary>按 remark 或 URL 模糊匹配移除订阅。</summary>
    public async Task RemoveSubscriptionAsync(string keyword)
    {
        var subs = await ListSubscriptionsAsync();
        var target = subs.FirstOrDefault(s => s.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        if (target == null) return;
        var remark = ExtractRemark(target);
        if (!string.IsNullOrEmpty(remark))
            await RunKnifeAsync($"subs remove \"{remark}\"");
    }

    public async Task<List<string>> ListSubscriptionsAsync()
    {
        var output = await RunKnifeAsync("subs show");
        // 旧框架没有 Split(char, StringSplitOptions) 重载，改用 Split(char[], StringSplitOptions)
        return output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    // ─────────────────────── 拉取 + 测速择优 ───────────────────────

    /// <summary>拉取全部订阅并测速择优，生成 valid_nodes.txt。</summary>
    public async Task<int> RefreshAndPrepareAsync(int topN = 10)
    {
        await RunKnifeAsync("subs fetch --all");

        var resultsPath = Path.Combine(_workingDir, "results.csv");
        var validPath = Path.Combine(_workingDir, "valid_nodes.txt");

        await RunKnifeAsync(
            $"http --from-db --speedtest --sort --type csv -o \"{resultsPath}\" --timeout 60");

        if (!File.Exists(resultsPath))
            throw new FileNotFoundException("测速结果文件未生成", resultsPath);

        var validLinks = File.ReadLines(resultsPath)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Split(','))
            .Where(p => p.Length >= 2)
            .OrderByDescending(p =>
            {
                // 旧框架不支持从末尾索引 ^1，改用显式 p.Length - 1
                var last = p[p.Length - 1];
                return double.TryParse(last, NumberStyles.Any, CultureInfo.InvariantCulture, out var s) ? s : 0;
            })
            .Take(topN)
            .Select(p => p[0].Trim('"'))
            .Where(link =>
                link.StartsWith("vless://") || link.StartsWith("vmess://") ||
                link.StartsWith("trojan://") || link.StartsWith("ss://"))
            .ToList();

        if (validLinks.Count == 0)
            throw new InvalidOperationException("测速后没有可用节点，请检查订阅是否有效。");

        // 旧框架没有 File.WriteAllLinesAsync，改用 Task.Run 包装同步 API
        await Task.Run(() => File.WriteAllLines(validPath, validLinks, Encoding.UTF8));
        return validLinks.Count;
    }

    // ─────────────────────── 自动更新定时器 ───────────────────────

    public void EnableAutoUpdate(int intervalHours, int topN)
    {
        DisableAutoUpdate();
        if (intervalHours <= 0) return;

        _autoUpdateEnabled = true;
        _autoUpdateTimer = new System.Timers.Timer(
            TimeSpan.FromHours(intervalHours).TotalMilliseconds) { AutoReset = true };
        _autoUpdateTimer.Elapsed += async (_, _) =>
        {
            if (!_autoUpdateEnabled) return;
            try
            {
                await RefreshAndPrepareAsync(topN);
                LogReceived?.Invoke($"[自动更新] {DateTime.Now:HH:mm} 订阅已刷新并重新择优。");
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[自动更新失败] {ex.Message}");
            }
        };
        _autoUpdateTimer.Start();
    }

    public void DisableAutoUpdate()
    {
        _autoUpdateEnabled = false;
        _autoUpdateTimer?.Stop();
        _autoUpdateTimer?.Dispose();
        _autoUpdateTimer = null;
    }

    // ─────────────────────── 代理生命周期 ───────────────────────

    public void StartProxy(int port = 1080, int rotateSeconds = 300)
    {
        StopProxy();
        ListenPort = port;
        var validPath = Path.Combine(_workingDir, "valid_nodes.txt");
        if (!File.Exists(validPath))
            throw new FileNotFoundException("未找到 valid_nodes.txt，请先刷新订阅并测速。", validPath);

        var psi = new ProcessStartInfo
        {
            FileName = _knifePath,
            Arguments = $"proxy inbound -f \"{validPath}\" --port {port} --rotate {rotateSeconds} --core \"{_corePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _workingDir
        };

        // 用局部变量消除 CS8602（_proxyProcess 可能为 null 的警告）
        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) LogReceived?.Invoke(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) LogReceived?.Invoke("[ERR] " + e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        _proxyProcess = proc;
    }

    public void StopProxy()
    {
        var proc = _proxyProcess;
        if (proc is { HasExited: false })
        {
            try
            {
                // 旧框架没有 Kill(bool)，改用无参 Kill()
                proc.Kill();
                proc.WaitForExit(3000);
            }
            catch { /* ignored */ }
        }
        proc?.Dispose();
        _proxyProcess = null;
    }

    // ─────────────────────── 内部工具 ───────────────────────

    private static string? ExtractRemark(string subsLine)
    {
        var idx = subsLine.IndexOf(']');
        if (idx > 0 && subsLine.StartsWith("["))
            return subsLine.Substring(1, idx - 1);
        var parts = subsLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    private async Task<string> RunKnifeAsync(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _knifePath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _workingDir
        };
        using var p = Process.Start(psi);
        if (p == null)
            throw new InvalidOperationException($"无法启动 xray-knife：{args}");

        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();

        // 旧框架没有 WaitForExitAsync，改用 Task.Run 包装同步版本
        await Task.Run(() => p.WaitForExit());

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"xray-knife 执行失败 ({args}):\n{stderr}\n{stdout}");
        return stdout;
    }

    public void Dispose()
    {
        DisableAutoUpdate();
        StopProxy();
    }
}
