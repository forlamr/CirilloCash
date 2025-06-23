using Android;
using Android.OS;
using Android.Provider;
using Android.Content;
using Android.Net;
using Microsoft.Maui.ApplicationModel;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace CirilloCash
{
    public static class StoragePermissionHelper
    {
        public const int RequestCode = 1000;

        public static bool HasManageAllFilesPermission()
            => Build.VERSION.SdkInt >= BuildVersionCodes.R && Android.OS.Environment.IsExternalStorageManager;

        public static void RequestManageAllFilesPermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                var uri = Android.Net.Uri.Parse($"package:{AppInfo.Current.PackageName}");
                var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission, uri);
                Platform.CurrentActivity.StartActivityForResult(intent, RequestCode);
            }
        }

        public static Task<bool> WaitForPermissionResult()
        {
            var tcs = new TaskCompletionSource<bool>();
            StoragePermissionCallback.OnResult = granted =>
            {
                tcs.TrySetResult(granted);
            };
            return tcs.Task;
        }
    }

    class StoragePermissionCallback
    {
        public static Action<bool> OnResult { get; set; }
    }
}
