using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VRSLAM.Libs
{
    public class AppPath
    {
        public static string HOME_DIR = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string MRVR_DIR = HOME_DIR + "/VRSLAM";
        public static string TOOLS_DIR = MRVR_DIR + "/tools";
        public static string TMP_DIR = MRVR_DIR + "/tmp";
        public static string OUTPUT_DIR = MRVR_DIR + "/output";

        public static bool IS_MAC_64 = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) && 
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;
        public static bool IS_MAC_ARM64 = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) && 
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64;        
        public static bool IS_WINDOWS_32 = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) && 
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X86;
        public static bool IS_WINDOWS_64 = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) && 
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;
        public static bool IS_LINUX_32 = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) && 
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X86;
        public static bool IS_LINUX_64 = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) && 
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;

        public static string APKTOOL_URL = "https://github.com/iBotPeaches/Apktool/releases/download/v2.11.1/apktool_2.11.1.jar";
        public static string UBER_APK_SIGNER_URL = "https://github.com/patrickfav/uber-apk-signer/releases/download/v1.3.0/uber-apk-signer-1.3.0.jar";
        public static string JDK_URL = 
            IS_WINDOWS_32 ? "https://cdn.azul.com/zulu/bin/zulu17.56.15-ca-jdk17.0.14-win_i686.zip" : 
            IS_WINDOWS_64 ? "https://cdn.azul.com/zulu/bin/zulu17.56.15-ca-jdk17.0.14-win_x64.zip" :
            IS_MAC_64 ? "https://cdn.azul.com/zulu/bin/zulu17.56.15-ca-jdk17.0.14-macosx_x64.zip" :
            IS_MAC_ARM64 ? "https://cdn.azul.com/zulu/bin/zulu17.56.15-ca-jdk17.0.14-macosx_aarch64.zip" :
            IS_LINUX_32 ? "https://cdn.azul.com/zulu/bin/zulu17.56.15-ca-jdk17.0.14-linux_i686.zip" :
            IS_LINUX_64 ? "https://cdn.azul.com/zulu/bin/zulu24.28.83-ca-jdk24.0.0-linux_x64.zip" : "";

        public static string PLATFORM_TOOLS_URL = 
            IS_WINDOWS_32 ? "https://dl.google.com/android/repository/platform-tools_r34.0.1-windows.zip" :
            IS_WINDOWS_64 ? "https://dl.google.com/android/repository/platform-tools_r34.0.1-windows.zip" :
            IS_MAC_64 ? "https://dl.google.com/android/repository/platform-tools_r34.0.1-darwin.zip" :
            IS_MAC_ARM64 ? "https://dl.google.com/android/repository/platform-tools_r34.0.1-darwin.zip" :
            IS_LINUX_32 ? "https://dl.google.com/android/repository/platform-tools_r34.0.1-linux.zip" :
            IS_LINUX_64 ? "https://dl.google.com/android/repository/platform-tools_r34.0.1-linux.zip" : "";
        
        public static string PLATFORM_TOOLS = TOOLS_DIR + "/platform-tools";
        public static string APKTOOL_PATH = TOOLS_DIR + "/apktool.jar";
        public static string JDK_PATH = TOOLS_DIR + "/jdk/bin/java";
        public static string UBER_APK_SIGNER_PATH = TOOLS_DIR + "/uber-apk-signer.jar";
    }
}