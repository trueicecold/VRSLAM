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
        static string folderPath = "";
        static string apkPath = "";
        public static async Task Fix(string _apkPath)
        {
            apkPath = _apkPath;
            fileName = Path.GetFileNameWithoutExtension(apkPath);
            folderPath = Path.GetDirectoryName(apkPath);
            
            DeleteFolders();
            await UnpackAPK();
            await ReplacePackageName();
            await PackAPK();
            await SignAPK();
        }

        static void DeleteFolders() {
            if (Directory.Exists(AppPath.TMP_DIR + "/" + fileName)) {
                Directory.Delete(AppPath.TMP_DIR + "/" + fileName, true);
            }

            if (Directory.Exists(AppPath.OUTPUT_DIR + "/" + fileName)) {
                Directory.Delete(AppPath.OUTPUT_DIR + "/" + fileName, true);
            }
        }

        static async Task UnpackAPK()
        {
            HTMLLogger.Log("Unpacking APK...");
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
            /*while (!process.StandardOutput.EndOfStream)
            {
                string line = process.StandardOutput.ReadLine();
                Console.WriteLine(line);
            }*/
            await process.WaitForExitAsync();
            process.Close();
        }

        static async Task ReplacePackageName()
        {
            HTMLLogger.Log("Replacing Package Name...");
            string manifestPath = AppPath.TMP_DIR + "/" + fileName + "/source/AndroidManifest.xml";
            string manifestContent = File.ReadAllText(manifestPath);
            
            string packageName = manifestContent.Split("package=\"")[1].Split("\"")[0];
            string packageNameSmali = packageName.Replace(".", "/");
            
            List<string> packageNameSplit = packageName.Split('.').ToList();
            packageNameSplit.Insert(1, "mrf");
            
            string newPackageName = string.Join(".", packageNameSplit);
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

        static async Task PackAPK() {
            HTMLLogger.Log("Packing APK...");
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
                /*while (!process.StandardOutput.EndOfStream)
                {
                    string line = process.StandardOutput.ReadLine();
                    Console.WriteLine(line);
                }*/
                await process.WaitForExitAsync();
                process.Close();
            }
        }

        static async Task SignAPK() {
            int logId = HTMLLogger.Log("Signing APK...");
            Console.WriteLine(AppPath.JDK_PATH + " -jar " + AppPath.UBER_APK_SIGNER_PATH + " -a \"" + AppPath.OUTPUT_DIR + "/" + fileName + "/" + fileName + ".apk\" -o \"" + AppPath.OUTPUT_DIR + "/" + fileName + "/" + fileName + ".fixed.apk\"");

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
            /*process.OutputDataReceived += new DataReceivedEventHandler ((sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                }
            });*/
            process.Exited += new EventHandler ((sender, e) =>
            {
                if (process.ExitCode != 0)
                {
                    HTMLLogger.Log("Signing APK... Error: " + process.ExitCode, logId);
                }
                else
                {
                    HTMLLogger.Log("Signing APK... Done", logId);
                    HTMLLogger.Log("APK fixed and signed: " + AppPath.OUTPUT_DIR + "/" + fileName + "/fixed.apk", logId);
                }
                process.Close();
                Console.WriteLine("Process exited.");
            });
            process.Start();
            process.BeginOutputReadLine();
            await process.WaitForExitAsync();
        /*while (!process.StandardOutput.EndOfStream)
        {
            string line = process.StandardOutput.ReadLine();
            Console.WriteLine(line);
        }*/
        //process.WaitForExit();
        //process.Close();
        }
    }
}