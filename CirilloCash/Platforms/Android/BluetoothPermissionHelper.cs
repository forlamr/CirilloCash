using Android;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace CirilloCash;

public static class BluetoothPermissionHelper
{
    public const int RequestCode = 1001;

    public static bool AreBluetoothPermissionsGranted()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
        {
            return true;
        }

        var connectGranted = ContextCompat.CheckSelfPermission(
            Platform.CurrentActivity,
            Manifest.Permission.BluetoothConnect) == Permission.Granted;

        var scanGranted = ContextCompat.CheckSelfPermission(
            Platform.CurrentActivity,
            Manifest.Permission.BluetoothScan) == Permission.Granted;

        return connectGranted && scanGranted;
    }

    public static void RequestBluetoothPermissions()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
        {
            BluetoothPermissionCallback.OnResult?.Invoke(true);
            return;
        }

        ActivityCompat.RequestPermissions(
            Platform.CurrentActivity,
            new[] { Manifest.Permission.BluetoothConnect, Manifest.Permission.BluetoothScan },
            RequestCode);
    }

    public static Task<bool> WaitForPermissionResult()
    {
        var tcs = new TaskCompletionSource<bool>();
        BluetoothPermissionCallback.OnResult = granted => tcs.TrySetResult(granted);
        return tcs.Task;
    }
}

public static class BluetoothPermissionCallback
{
    public static Action<bool>? OnResult { get; set; }
}
