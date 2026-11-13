using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiHAva.Desktop.Services;

/// <summary>
/// 仿照 SourceGit 实现的跨平台进程单例服务（基于排他文件锁 + 本地命名管道 IPC）
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    public bool IsFirstInstance { get; private set; }

    public event Action<string>? MessageReceived;

    private readonly string _lockFilePath;
    private FileStream? _singletonLock;
    private NamedPipeServerStream? _server;
    private CancellationTokenSource? _cancellationTokenSource;

    public SingleInstanceService()
    {
        IsFirstInstance = false;
        _lockFilePath = GetLockFilePath();

        try
        {
            var dir = Path.GetDirectoryName(_lockFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 通过排他文件流尝试加锁 (FileShare.None)
            // 若已有实例在运行，则会抛出 IOException，判定为非首个实例
            _singletonLock = new FileStream(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            IsFirstInstance = true;
        }
        catch
        {
            IsFirstInstance = false;
        }

        if (IsFirstInstance)
        {
            try
            {
                _server = new NamedPipeServerStream(
                    GetPipeName(),
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(StartServerAsync);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SingleInstance] Failed to start pipe server: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 当检测到非首个实例时，将命令参数发送给已在运行的首个实例
    /// </summary>
    public void SendToFirstInstance(string cmd)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            client.Connect(1500);
            if (!client.IsConnected) return;

            using (var writer = new StreamWriter(client, Encoding.UTF8))
            {
                writer.WriteLine(cmd);
                writer.Flush();
            }

            if (OperatingSystem.IsWindows())
            {
                client.WaitForPipeDrain();
            }
            else
            {
                Thread.Sleep(500);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SingleInstance] Send to first instance failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _server?.Dispose();
            _singletonLock?.Dispose();

            if (IsFirstInstance && File.Exists(_lockFilePath))
            {
                File.Delete(_lockFilePath);
            }
        }
        catch
        {
            // 忽略清理异常
        }
    }

    private static string GetLockFilePath()
    {
        string baseDir;
        if (OperatingSystem.IsLinux())
        {
            var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            baseDir = !string.IsNullOrEmpty(runtimeDir) && Directory.Exists(runtimeDir)
                ? runtimeDir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "mijia-api");
        }
        else
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "mijia-api");
        }

        return Path.Combine(baseDir, "mihava.lock");
    }

    private string GetPipeName()
    {
        // 基于用户名和锁目录哈希，生成唯一且符合规范的命名管道名称
        var lockDir = Path.GetDirectoryName(_lockFilePath) ?? "mihava";
        var normalized = lockDir.Replace('\\', '/').TrimEnd('/');
        var hashSource = $"{Environment.UserName}_{normalized}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashSource)))[..10];
        return $"MiHAva_{hash}";
    }

    private async Task StartServerAsync()
    {
        if (_server == null || _cancellationTokenSource == null) return;

        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                await _server.WaitForConnectionAsync(_cancellationTokenSource.Token);

                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    using var reader = new StreamReader(_server, Encoding.UTF8, leaveOpen: true);
                    var line = await reader.ReadLineAsync(_cancellationTokenSource.Token);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        MessageReceived?.Invoke(line.Trim());
                    }
                }

                _server.Disconnect();
            }
            catch
            {
                if (_server is { IsConnected: true } && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _server.Disconnect();
                }
            }
        }
    }
}
