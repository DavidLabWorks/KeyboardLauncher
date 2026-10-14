using KeyboardLauncher.Core;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel;
using Windows.Services.Store;

namespace KeyboardLauncher;

internal sealed class StoreIntegration
{
#if STORE
    internal static bool Required => true;
#else
    internal static bool Required => false;
#endif
    internal static bool IsPackaged
    {
        get { try { _ = Package.Current.Id; return true; } catch (InvalidOperationException) { return false; } }
    }
    internal AppLicense? License { get; private set; }
    internal bool Available => context != null;
    internal bool CanUse => !Required || License?.CanUse(DateTimeOffset.UtcNow) == true;
    internal string? Error { get; private set; }
    internal event Action? Changed;
    private StoreContext? context;
    private Task? refresh;

    internal async Task InitializeAsync(nint window, DispatcherQueue dispatcher)
    {
        if (!Required) return;
        if (!IsPackaged) { Error = "请从 Microsoft Store 安装此版本。"; return; }
        if (Package.Current.Id.Name == "KeyboardLauncher.LocalPreview")
        {
            Error = "本地预览包尚未关联商店，试用和购买暂不可用。";
            return;
        }
        context = StoreContext.GetDefault();
        WinRT.Interop.InitializeWithWindow.Initialize(context, window);
        context.OfflineLicensesChanged += (_, _) => dispatcher.TryEnqueue(async () =>
        {
            if (refresh != null) await refresh;
            await RefreshAsync();
        });
        await RefreshAsync();
    }

    internal Task RefreshAsync() => context == null ? Task.CompletedTask : refresh ??= RefreshCoreAsync();
    private async Task RefreshCoreAsync()
    {
        // Yield before assigning refresh so even a synchronous failure can be retried.
        await Task.Yield();
        try
        {
            if (context == null) throw new InvalidOperationException();
            var license = await context.GetAppLicenseAsync();
            License = new AppLicense(license.IsActive, license.IsTrial, license.ExpirationDate);
            Error = null;
        }
        catch { License = null; Error = "无法验证商店授权，请检查网络并重试。"; }
        finally { refresh = null; Changed?.Invoke(); }
    }

    internal async Task<string?> PurchaseAsync(nint window)
    {
        if (context == null) return Error ?? "请从 Microsoft Store 安装此版本。";
        try
        {
            WinRT.Interop.InitializeWithWindow.Initialize(context, window);
            var result = await context.GetStoreProductForCurrentAppAsync();
            if (result.ExtendedError != null || result.Product == null) return "此应用尚未关联商店，暂时无法购买。";
            var purchase = await result.Product.RequestPurchaseAsync();
            if (purchase.Status is StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased)
            {
                if (refresh != null) await refresh;
                await RefreshAsync();
                return License is { IsActive: true, IsTrial: false } ? null : "无法验证商店授权，请检查网络并重试。";
            }
            return purchase.Status == StorePurchaseStatus.NotPurchased ? "购买已取消。" : "购买未完成，请稍后重试。";
        }
        catch { return "购买未完成，请稍后重试。"; }
    }

    internal string Status => !Required ? L.T("本地开发版本，不代表商店授权。") :
        Error != null ? L.T(Error) :
        License is { IsActive: true, IsTrial: false } ? L.T("已购买完整版") :
        License?.CanUse(DateTimeOffset.UtcNow) == true ? L.F("试用至 {0}", License.Expiration.ToLocalTime().ToString("g")) :
        L.T("试用已结束或尚未取得授权，请购买完整版或恢复购买。");

    internal static async Task<bool> StartupEnabledAsync()
    {
        var task = await StartupTask.GetAsync("KeyboardLauncherStartup");
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    internal static async Task SetStartupAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync("KeyboardLauncherStartup");
        if (!enabled) { task.Disable(); return; }
        var state = await task.RequestEnableAsync();
        if (state is not (StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy))
            throw new InvalidOperationException(L.T("登录启动被 Windows 禁用，请前往设置 → 应用 → 启动检查；组织策略限制需联系管理员。"));
    }
}
