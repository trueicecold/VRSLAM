using Photino.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRSLAM.Libs;

namespace VRSLAM.Managers
{
    public class DependencyManager
    {
        public static async Task Download()
        {
            await DownloadJDK();
            await DownloadPlatformTools();
            await DownloadAPKTool();
            await DownloadAPKSigner();
            await DownloadRClone();
            CheckDependencies();
        }

        public static void CheckDependencies() {
            List<object> dependencies = new List<object>();

            if (File.Exists(AppPath.TOOLS_DIR + "/jdk/bin/java" + (AppPath.IS_WINDOWS_32 || AppPath.IS_WINDOWS_64 ? ".exe" : "")))
            {
                dependencies.Add(new {
                    dependency = "JDK",
                    found = true
                });
            }
            else {
                dependencies.Add(new {
                    dependency = "JDK",
                    found = false
                });
            }
            if (File.Exists(AppPath.TOOLS_DIR + "/platform-tools/adb" + (AppPath.IS_WINDOWS_32 || AppPath.IS_WINDOWS_64 ? ".exe" : "")))
            {
                dependencies.Add(new {
                    dependency = "Platform Tools",
                    found = true
                });
            }
            else {
                dependencies.Add(new {
                    dependency = "Platform Tools",
                    found = false
                });
            }
            if (File.Exists(AppPath.TOOLS_DIR + "/apktool.jar"))
            {
                dependencies.Add(new {
                    dependency = "APKTool",
                    found = true
                });
            }
            else {
                dependencies.Add(new {
                    dependency = "APKTool",
                    found = false
                });
            }
            if (File.Exists(AppPath.TOOLS_DIR + "/uber-apk-signer.jar"))
            {
                dependencies.Add(new {
                    dependency = "APK Signer",
                    found = true
                });
            }
            else {
                dependencies.Add(new {
                    dependency = "APK Signer",
                    found = false
                });
            }
            if (File.Exists(AppPath.TOOLS_DIR + "/rclone" + (AppPath.IS_WINDOWS_32 || AppPath.IS_WINDOWS_64 ? ".exe" : "")))
            {
                dependencies.Add(new {
                    dependency = "RClone",
                    found = true
                });
            }
            else {
                dependencies.Add(new {
                    dependency = "RClone",
                    found = false
                });
            }
            Shared.Window.SendWebMessage(JSON.Stringify(new {
                type = "check_dependencies",
                dependencies = dependencies
            }));
        }

        static async Task DownloadJDK()
        {
            string jdkUrl = AppPath.JDK_URL;
            string jdkPath = AppPath.TOOLS_DIR + "/jdk.zip";
            string javaFile = AppPath.TOOLS_DIR + "/jdk/bin/java";
            if (!File.Exists(javaFile))
            {
                int logId = HTMLLogger.Log("Downloading JDK...");
                FileDownloader downloader = new FileDownloader();
                downloader.ProgressChanged += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading JDK... " + e.ProgressPercentage + "%", logId);
                };
                downloader.DownloadCompleted += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading JDK... Done", logId);
                    System.IO.Compression.ZipFile.ExtractToDirectory(jdkPath, AppPath.TOOLS_DIR + "/jdk", true);
                    File.Delete(jdkPath);
                    HTMLLogger.Log("Extracting JDK... Done", logId);
                    if (AppPath.IS_MAC_ARM64 || AppPath.IS_MAC_64)
                    {
                        var directories = Directory.GetDirectories(AppPath.TOOLS_DIR + "/jdk").ToList();
                        Directory.Move(directories[0] + "/zulu-17.jdk/Contents/Home", AppPath.TOOLS_DIR + "/jdk_tmp");
                        Directory.Delete(AppPath.TOOLS_DIR + "/jdk", true);
                        Directory.Move(AppPath.TOOLS_DIR + "/jdk_tmp", AppPath.TOOLS_DIR + "/jdk");
                    }
                    else {
                        var directories = Directory.GetDirectories(AppPath.TOOLS_DIR + "/jdk").ToList();
                        Directory.Move(directories[0], AppPath.TOOLS_DIR + "/jdk_tmp");
                        Directory.Delete(AppPath.TOOLS_DIR + "/jdk", true);
                        Directory.Move(AppPath.TOOLS_DIR + "/jdk_tmp", AppPath.TOOLS_DIR + "/jdk");
                    }
                };
                downloader.Error += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading JDK... Error: " + e.StatusCode, logId);
                };
                await downloader.DownloadFileAsync(jdkUrl, jdkPath);
            }
        }

        static async Task DownloadPlatformTools()
        {
            string platformToolsUrl = AppPath.PLATFORM_TOOLS_URL;
            string platformToolsPath = AppPath.TOOLS_DIR + "/platform-tools.zip";
            string adbFile = AppPath.TOOLS_DIR + "/platform-tools/adb";
            if (!File.Exists(adbFile))
            {
                int logId = HTMLLogger.Log("Downloading Platform Tools...");
                FileDownloader downloader = new FileDownloader();
                downloader.ProgressChanged += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading Platform Tools... " + e.ProgressPercentage + "%", logId);
                };
                downloader.DownloadCompleted += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading Platform Tools... Done", logId);
                    System.IO.Compression.ZipFile.ExtractToDirectory(platformToolsPath, AppPath.TOOLS_DIR, true);
                    File.Delete(platformToolsPath);
                    HTMLLogger.Log("Extracting Platform Tools... Done", logId);
                };
                downloader.Error += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading Platform Tools... Error: " + e.StatusCode, logId);
                };
                await downloader.DownloadFileAsync(platformToolsUrl, platformToolsPath);
            }
        }

        static async Task DownloadAPKTool()
        {
            string apktoolUrl = AppPath.APKTOOL_URL;
            string apktoolPath = AppPath.TOOLS_DIR + "/apktool.jar";
            if (!File.Exists(apktoolPath))
            {
                int logId = HTMLLogger.Log("Downloading APKTool...");
                FileDownloader downloader = new FileDownloader();
                downloader.ProgressChanged += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading APKTool... " + e.ProgressPercentage + "%", logId);
                };
                downloader.DownloadCompleted += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading APKTool... Done", logId);
                };
                downloader.Error += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading APKTool... Error: " + e.StatusCode, logId);
                };
                await downloader.DownloadFileAsync(apktoolUrl, apktoolPath);
            }
        }

        static async Task DownloadAPKSigner()
        {
            string apkSignerUrl = AppPath.UBER_APK_SIGNER_URL;
            string apkSignerPath = AppPath.TOOLS_DIR + "/uber-apk-signer.jar";
            if (!File.Exists(apkSignerPath))
            {
                int logId = HTMLLogger.Log("Downloading APK Signer...");
                FileDownloader downloader = new FileDownloader();
                downloader.ProgressChanged += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading APK Signer... " + e.ProgressPercentage + "%", logId);
                };
                downloader.DownloadCompleted += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading APK Signer... Done", logId);
                };
                downloader.Error += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading APK Signer... Error: " + e.StatusCode, logId);
                };
                await downloader.DownloadFileAsync(apkSignerUrl, apkSignerPath);
            }
        }

        static async Task DownloadRClone() {
            string rcloneUrl = AppPath.RCLONE_URL;
            string rclonePath = AppPath.TOOLS_DIR + "/rclone.zip";
            string rcloneFile = AppPath.TOOLS_DIR + "/rclone" + (AppPath.IS_WINDOWS_32 || AppPath.IS_WINDOWS_64 ? ".exe" : "");
            if (!File.Exists(rcloneFile))
            {
                int logId = HTMLLogger.Log("Downloading RClone...");
                FileDownloader downloader = new FileDownloader();
                downloader.ProgressChanged += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading RClone... " + e.ProgressPercentage + "%", logId);
                };
                downloader.DownloadCompleted += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading RClone... Done", logId);
                    System.IO.Compression.ZipFile.ExtractToDirectory(rclonePath, AppPath.TOOLS_DIR + "/rclone", true);
                    var directories = Directory.GetDirectories(AppPath.TOOLS_DIR + "/rclone").ToList();
                    Directory.Move(directories[0], AppPath.TOOLS_DIR + "/rclone_tmp");
                    Directory.Delete(AppPath.TOOLS_DIR + "/rclone", true);
                    Directory.Move(AppPath.TOOLS_DIR + "/rclone_tmp", AppPath.TOOLS_DIR + "/rclone");
                    File.Delete(rclonePath);
                    HTMLLogger.Log("Extracting RClone... Done", logId);
                };
                downloader.Error += (sender, e) =>
                {
                    HTMLLogger.Log("Downloading RClone... Error: " + e.StatusCode, logId);
                };
                await downloader.DownloadFileAsync(rcloneUrl, rclonePath);
            }
        }
    }
}