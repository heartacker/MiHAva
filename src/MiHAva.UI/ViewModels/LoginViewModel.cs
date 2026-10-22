using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiHAva.UI.Services;
using MiJia.Net.Auth;
using MiJia.Net.Models.Auth;

namespace MiHAva.UI.ViewModels;

public partial class LoginViewModel : ObservableObject, IDisposable
{
    private readonly IMiAuthService _authService;
    private CancellationTokenSource? _pollCts;

    [ObservableProperty]
    private Bitmap? _qrBitmap;

    [ObservableProperty]
    private string _statusMessage = "正在获取米家登录二维码...";

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isExpired = false;

    public event Action<MiAuthTokens>? LoginSucceeded;

    public LoginViewModel(IMiAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// 开始请求二维码并轮询
    /// </summary>
    [RelayCommand]
    public async Task StartLoginAsync()
    {
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource();

        try
        {
            IsLoading = true;
            IsExpired = false;
            StatusMessage = "正在请求米家登录二维码...";

            var qrResult = await _authService.RequestLoginQrCodeAsync(_pollCts.Token);
            if (qrResult.QrBytes != null && qrResult.QrBytes.Length > 0)
            {
                QrBitmap = QrCodeGenerator.FromBytes(qrResult.QrBytes);
            }
            else
            {
                QrBitmap = QrCodeGenerator.GenerateQrBitmap(qrResult.QrContent);
            }
            StatusMessage = "请使用米家 APP 扫描二维码登录";
            IsLoading = false;

            // 开始后台长轮询
            _ = PollStatusLoopAsync(qrResult, _pollCts.Token);
        }
        catch (OperationCanceledException)
        {
            // 用户取消
        }
        catch (Exception ex)
        {
            IsLoading = false;
            StatusMessage = $"获取二维码失败: {ex.Message}";
        }
    }

    private async Task PollStatusLoopAsync(LoginQrCodeResult qrResult, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, token);
                var pollResult = await _authService.PollQrLoginStatusAsync(qrResult, token);

                if (pollResult.State == QrLoginState.ScannedWaitingConfirm)
                {
                    StatusMessage = "已扫码，请在手机端点击【确认登录】...";
                }
                else if (pollResult.State == QrLoginState.Confirmed && pollResult.Tokens != null)
                {
                    StatusMessage = "登录成功，正在进入桌面端...";
                    await _authService.SaveTokensAsync(pollResult.Tokens, cancellationToken: token);
                    LoginSucceeded?.Invoke(pollResult.Tokens);
                    break;
                }
                else if (pollResult.State == QrLoginState.Expired)
                {
                    IsExpired = true;
                    StatusMessage = "二维码已失效，点击刷新重试";
                    break;
                }
                else if (pollResult.State == QrLoginState.Failed)
                {
                    StatusMessage = $"扫码失败: {pollResult.Message}";
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusMessage = $"网络请求异常: {ex.Message}";
                await Task.Delay(3000, token);
            }
        }
    }

    public void Dispose()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
    }
}
