using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace CirilloCash
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == BluetoothPermissionHelper.RequestCode)
            {
                var granted = grantResults.Length > 0 && grantResults.All(result => result == Permission.Granted);
                BluetoothPermissionCallback.OnResult?.Invoke(granted);
            }
        }
    }
}
