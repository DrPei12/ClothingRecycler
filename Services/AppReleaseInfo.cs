using System.Reflection;

namespace ClothingRecycler.Desktop.Services
{
    public static class AppReleaseInfo
    {
        public static string VersionText
        {
            get
            {
                var infoVersion = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                if (!string.IsNullOrWhiteSpace(infoVersion))
                {
                    return infoVersion.Split('+')[0];
                }

                var version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version is null)
                {
                    return "1.0.7";
                }

                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        public static string PackagingText => "Windows 本地桌面版（用户级安装 / 文件夹发布）";
    }
}
