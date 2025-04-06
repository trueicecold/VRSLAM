using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VRSLAM.Libs;

namespace VRSLAM.Libs
{
    public class APKFixer
    {
        static string fileName = "";
        static string packageName = "";
        static string newPackageName = "";
        static string folderPath = "";
        static string apkPath = "";
        static string newApkPath = "";
        public static async Task Rename(string _apkPath)
        {
            apkPath = _apkPath;
            fileName = Path.GetFileNameWithoutExtension(apkPath);
            folderPath = Path.GetDirectoryName(apkPath);
            
            bool isOK = true;

            DeleteFolders();
            isOK = await UnpackAPK();
            if (!isOK) return;
            isOK = await ReplacePackageName();
            if (!isOK) return;
            isOK = await PackAPK();
            if (!isOK) return;
            isOK = await SignAPK();
            if (isOK) {
                HTMLLogger.Success("APK Renamed Successfully <button class='button info' onclick='installRenamedAPK();'>Install Fixed APK</button>");
                Shared.Window.SendWebMessage(JSON.Stringify(new {
                    type = "apk_renamed",
                    filePath = newApkPath
                }));
            }
            else {
                HTMLLogger.Error("APK Renaming Failed");
            }
        }

        static void DeleteFolders() {
            if (Directory.Exists(AppPath.TMP_DIR + "/" + fileName)) {
                Directory.Delete(AppPath.TMP_DIR + "/" + fileName, true);
            }

            if (Directory.Exists(AppPath.OUTPUT_DIR + "/" + fileName)) {
                Directory.Delete(AppPath.OUTPUT_DIR + "/" + fileName, true);
            }
        }

        static async Task<bool> UnpackAPK()
        {
            int logId = HTMLLogger.Log("Unpacking APK...");
            var process = new Process {
                StartInfo = new ProcessStartInfo
                {
                    FileName = AppPath.JDK_PATH,
                    Arguments = " -jar " + AppPath.APKTOOL_PATH + " -f d \"" + apkPath + "\" -o \"" + AppPath.TMP_DIR + "/" + fileName + "/source\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                HTMLLogger.Log("Unpacking APK... Error: " + process.ExitCode, logId);
                return false;
            }
            else
            {
                HTMLLogger.Log("Unpacking APK... Done", logId);
                process.Close();
                return true;
            }
        }

        static async Task<bool> ReplacePackageName()
        {
            HTMLLogger.Log("Replacing Package Name...");
            try {
                string manifestPath = AppPath.TMP_DIR + "/" + fileName + "/source/AndroidManifest.xml";
                string manifestContent = File.ReadAllText(manifestPath);
                
                packageName = manifestContent.Split("package=\"")[1].Split("\"")[0];
                string packageNameSmali = packageName.Replace(".", "/");
                
                List<string> packageNameSplit = packageName.Split('.').ToList();
                packageNameSplit.Insert(1, "mrf");
                
                newPackageName = string.Join(".", packageNameSplit);
                string newPackageNameSmali = string.Join("/", packageNameSplit);

                //Replace occurunces of old package name in all relevant files
                string[] files = Directory.GetFiles(AppPath.TMP_DIR + "/" + fileName + "/source", "*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    if (file.EndsWith(".smali"))
                    {
                        string fileContent = File.ReadAllText(file);
                        if (fileContent.Contains(packageNameSmali))
                        {
                            Console.WriteLine("Replacing package name in: " + file);
                            fileContent = fileContent.Replace(packageNameSmali, newPackageNameSmali);
                        }
                        if (fileContent.Contains(packageName))
                        {
                            Console.WriteLine("Replacing package name in: " + file);
                            fileContent = fileContent.Replace(packageName, newPackageName);
                        }
                        fileContent = fileContent.Replace(packageNameSmali, newPackageNameSmali);
                        File.WriteAllText(file, fileContent);
                    }
                    else if (file.EndsWith(".xml"))
                    {
                        string fileContent = File.ReadAllText(file);
                        if (fileContent.Contains(packageName))
                        {
                            Console.WriteLine("Replacing package name in: " + file);
                            fileContent = fileContent.Replace(packageName, newPackageName);
                        }
                        File.WriteAllText(file, fileContent);
                    }
                }
                
                //Change folder structure to match new package name
                Directory.CreateDirectory(AppPath.TMP_DIR + "/" + fileName + "/source/smali/" + packageNameSplit[0] + "/mrf");
                Directory.Move(AppPath.TMP_DIR + "/" + fileName + "/source/smali/" + packageNameSplit[0] + "/" + packageNameSplit[2], AppPath.TMP_DIR + "/" + fileName + "/source/smali/" + packageNameSplit[0] + "/mrf/" + packageNameSplit[2]);

                HTMLLogger.Log("Replacing OBB Files...");
                if (Directory.Exists(folderPath + "/obb"))
                {
                    Directory.CreateDirectory(AppPath.OUTPUT_DIR + "/" + fileName + "/obb");
                    //Copy obb contents to new location and change file name to new package name
                    string[] obbFiles = Directory.GetFiles(folderPath + "/obb", "*", SearchOption.AllDirectories);
                    foreach (string obbFile in obbFiles)
                    {
                        string obbFileName = obbFile.Split('/').Last();
                        string newObbFileName = obbFileName.Replace(packageName, newPackageName);
                        File.Copy(obbFile, AppPath.OUTPUT_DIR + "/" + fileName + "/obb/" + newObbFileName);
                    }
                }
            }
            catch (Exception e)
            {
                HTMLLogger.Error("Replacing Package Name... Error: " + e.Message);
                return false;
            }
            return true;
        }

        static async Task<bool> PackAPK() {
            int logId = HTMLLogger.Log("Packing APK...");
            try {
                if (Directory.Exists(AppPath.TMP_DIR + "/" + fileName))
                {
                    var process = new Process {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = AppPath.JDK_PATH,
                            Arguments = " -jar " + AppPath.APKTOOL_PATH + " -f b \"" + AppPath.TMP_DIR + "/" + fileName + "/source\" -o \"" + AppPath.OUTPUT_DIR + "/" + fileName + "/" + fileName + ".apk\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        HTMLLogger.Error("Packing APK... Error: " + process.ExitCode, logId);
                        process.Close();
                        return false;
                    }
                    else
                    {
                        HTMLLogger.Log("Packing APK... Done", logId);
                        process.Close();
                        return true;
                    }
                }
                else {
                    HTMLLogger.Error("Packing APK... Error: Cannot Find Target Directory", logId);
                    return false;
                }
            }
            catch (Exception e)
            {
                HTMLLogger.Error("Packing APK... Error: " + e.Message, logId);
                return false;
            }
        }

        static async Task<bool> SignAPK() {
            int logId = HTMLLogger.Log("Signing APK...");
            try {
                var process = new Process {
                    StartInfo = new ProcessStartInfo
                        {
                            FileName = AppPath.JDK_PATH,
                            Arguments = " -jar " + AppPath.UBER_APK_SIGNER_PATH + " -a \"" + AppPath.OUTPUT_DIR + "/" + fileName + "/" + fileName + ".apk\" -o \"" + AppPath.OUTPUT_DIR + "/" + fileName + "/fixed",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    HTMLLogger.Error("Signing APK... Error: " + process.ExitCode, logId);
                    process.Close();
                    return false;
                }
                else
                {
                    process.Close();
                    HTMLLogger.Log("Signing APK... Done", logId);
                    //Move the fixed APK to the output folder
                    File.Move(AppPath.OUTPUT_DIR + "/" + fileName + "/fixed/" + fileName + "-aligned-debugSigned.apk", AppPath.OUTPUT_DIR + "/" + fileName + "/" + newPackageName + ".apk");
                    Directory.Delete(AppPath.OUTPUT_DIR + "/" + fileName + "/fixed", true);
                    //Delete the original APK
                    File.Delete(AppPath.OUTPUT_DIR + "/" + fileName + "/" + fileName + ".apk");
                    HTMLLogger.Log("Renamed APK Exported To:<br/>" + AppPath.OUTPUT_DIR + "/" + fileName + "/", logId);
                    newApkPath = AppPath.OUTPUT_DIR + "/" + fileName + "/" + newPackageName + ".apk";
                    return true;
                }
            }
            catch (Exception e)
            {
                HTMLLogger.Error("Signing APK... Error: " + e.Message, logId);
                return false;
            }
        }
    }
}