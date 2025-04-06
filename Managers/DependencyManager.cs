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
        }

        public void CheckDependency() {
            if (!File.Exists(AppPath.TOOLS_DIR + "/jdk/bin/java"))
            {
                HTMLLogger.Log("JDK not found");
                DownloadJDK();
            }
            else {
                HTMLLogger.Log("JDK found");
            }
            if (!File.Exists(AppPath.TOOLS_DIR + "/platform-tools/adb"))
            {
                HTMLLogger.Log("Platform Tools not found");
                DownloadPlatformTools();
            }
            if (!File.Exists(AppPath.TOOLS_DIR + "/apktool.jar"))
            {
                HTMLLogger.Log("APKTool not found");
                DownloadAPKTool();
            }
            if (!File.Exists(AppPath.TOOLS_DIR + "/uber-apk-signer.jar"))
            {
                HTMLLogger.Log("APK Signer not found");
                DownloadAPKSigner();
            }
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
    }
}