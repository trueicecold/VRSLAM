using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VRSLAM.Managers;

namespace VRSLAM.Libs
{
    /// <summary>
    /// Wrapper class for Android Debug Bridge (ADB) commands
    /// </summary>
    public class AdbToolkit : IDisposable
    {
        #region Events

        // Status change events
        public event EventHandler<DeviceStatusChangedEventArgs> DeviceConnected;
        public event EventHandler<DeviceStatusChangedEventArgs> DeviceDisconnected;
        public event EventHandler<PackageStatusEventArgs> PackageInstalled;
        public event EventHandler<PackageStatusEventArgs> PackageUninstalled;
        
        // Progress events
        public event EventHandler<ProgressEventArgs> ProgressChanged;
        
        // Error events
        public event EventHandler<AdbErrorEventArgs> ErrorOccurred;
        
        // Command events
        public event EventHandler<AdbCommandEventArgs> CommandExecuting;
        public event EventHandler<AdbCommandEventArgs> CommandExecuted;

        #endregion

        #region Event Args Classes

        public class DeviceStatusChangedEventArgs : EventArgs
        {
            public string DeviceId { get; }
            public Dictionary<string, object> DeviceInfo { get; }
            public DateTime Timestamp { get; }

            public DeviceStatusChangedEventArgs(string deviceId, Dictionary<string, object> deviceInfo)
            {
                DeviceId = deviceId;
                DeviceInfo = deviceInfo;
                Timestamp = DateTime.Now;
            }
        }

        public class PackageStatusEventArgs : EventArgs
        {
            public string DeviceId { get; }
            public string PackageName { get; }
            public DateTime Timestamp { get; }

            public PackageStatusEventArgs(string deviceId, string packageName)
            {
                DeviceId = deviceId;
                PackageName = packageName;
                Timestamp = DateTime.Now;
            }
        }

        public class ProgressEventArgs : EventArgs
        {
            public int Percentage { get; }
            public string Message { get; }

            public ProgressEventArgs(int percentage, string message)
            {
                Percentage = percentage;
                Message = message;
            }
        }

        public class AdbErrorEventArgs : EventArgs
        {
            public string ErrorMessage { get; }
            public string Command { get; }
            public Exception Exception { get; }

            public AdbErrorEventArgs(string errorMessage, string command, Exception exception = null)
            {
                ErrorMessage = errorMessage;
                Command = command;
                Exception = exception;
            }
        }

        public class AdbCommandEventArgs : EventArgs
        {
            public string Command { get; }
            public string Output { get; }
            public DateTime Timestamp { get; }

            public AdbCommandEventArgs(string command, string output = null)
            {
                Command = command;
                Output = output;
                Timestamp = DateTime.Now;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Path to the ADB executable
        /// </summary>
        public string AdbPath { get; private set; }

        /// <summary>
        /// Timeout for ADB commands in milliseconds
        /// </summary>
        public int CommandTimeout { get; set; } = 30000;

        /// <summary>
        /// Flag to enable device monitoring
        /// </summary>
        public bool MonitorDevices { get; private set; }

        /// <summary>
        /// Currently connected devices
        /// </summary>
        List<Dictionary<string, object>> ConnectedDevices { get; set; } = new List<Dictionary<string, object>>();

        #endregion

        #region Private Fields

        private CancellationTokenSource _monitorCts;
        private bool _disposed = false;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the ADB wrapper
        /// </summary>
        /// <param name="adbPath">Path to ADB executable. If null, tries to find ADB in PATH or in common locations</param>
        /// <param name="startServer">Whether to start the ADB server automatically</param>
        /// <param name="monitorDevices">Whether to start monitoring devices automatically</param>
        public AdbToolkit(string adbPath = null, bool startServer = true, bool monitorDevices = true)
        {
            AdbPath = AppPath.PLATFORM_TOOLS + "/adb" + (AppPath.IS_WINDOWS_32 || AppPath.IS_WINDOWS_64 ? ".exe" : "");
            
            if (string.IsNullOrEmpty(AdbPath))
            {
                throw new FileNotFoundException("ADB executable not found. Please specify a valid path.");
            }

            if (startServer)
            {
                StartServer();
            }

            if (monitorDevices)
            {
                StartMonitoringDevices();
            }
        }

        #endregion

        #region Public Methods - Server Management

        /// <summary>
        /// Starts the ADB server
        /// </summary>
        /// <returns>True if the server was started successfully</returns>
        public bool StartServer()
        {
            try
            {
                string output = ExecuteAdbCommand("start-server");
                return output.Contains("started successfully");
            }
            catch (Exception ex)
            {
                OnErrorOccurred("Failed to start ADB server", "start-server", ex);
                return false;
            }
        }

        /// <summary>
        /// Kills the ADB server
        /// </summary>
        /// <returns>True if the server was killed successfully</returns>
        public bool KillServer()
        {
            try
            {
                string output = ExecuteAdbCommand("kill-server");
                return !string.IsNullOrEmpty(output);
            }
            catch (Exception ex)
            {
                OnErrorOccurred("Failed to kill ADB server", "kill-server", ex);
                return false;
            }
        }

        /// <summary>
        /// Restarts the ADB server
        /// </summary>
        /// <returns>True if the server was restarted successfully</returns>
        public bool RestartServer()
        {
            return KillServer() && StartServer();
        }

        #endregion

        #region Public Methods - Device Management

        /// <summary>
        /// Starts monitoring for device connections/disconnections
        /// </summary>
        public void StartMonitoringDevices()
        {
            if (MonitorDevices)
                return;

            MonitorDevices = true;
            _monitorCts = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                try
                {
                    List<Dictionary<string, object>> previousDevices = new List<Dictionary<string, object>>();
                    List<string> previousDevicesIds = new List<string>();

                    while (!_monitorCts.Token.IsCancellationRequested)
                    {
                        List<string> currentDevicesIds = GetDevicesIds();
                        
                        // Check for newly connected devices
                        foreach (string device in currentDevicesIds)
                        {
                            if (!previousDevicesIds.Contains(device))
                            {
                                // Get device model
                                Dictionary<string, object> info = GetDeviceInfo(device);
                                OnDeviceConnected(device, info);
                                ConnectedDevices.Add(info);
                            }
                        }
                        
                        // Check for disconnected devices
                        foreach (string device in previousDevicesIds)
                        {
                            if (!currentDevicesIds.Contains(device))
                            {
                                string model = "Unknown"; // Device is disconnected, so we can't get model
                                OnDeviceDisconnected(device, null);
                                ConnectedDevices.RemoveAll(info => info["id"] == device);
                            }
                        }
                        
                        previousDevicesIds = currentDevicesIds;
                        await Task.Delay(1000, _monitorCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (Exception ex)
                {
                    OnErrorOccurred("Error monitoring devices", "devices", ex);
                }
                finally
                {
                    MonitorDevices = false;
                }
            }, _monitorCts.Token);
        }

        /// <summary>
        /// Stops monitoring for device connections/disconnections
        /// </summary>
        public void StopMonitoringDevices()
        {
            if (!MonitorDevices)
                return;

            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;
            MonitorDevices = false;
        }

        /// <summary>
        /// Gets a list of connected device IDs
        /// </summary>
        /// <returns>List of device IDs</returns>
        public List<string> GetDevicesIds()
        {
            List<string> devices = new List<string>();
            string output = ExecuteAdbCommand("devices");

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++) // Skip the first line ("List of devices attached")
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    string[] parts = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[1] == "device")
                    {
                        devices.Add(parts[0]);
                    }
                }
            }

            return devices;
        }

        /// <summary>
        /// Gets detailed information about connected devices
        /// </summary>
        /// <returns>Dictionary of device IDs mapped to their properties</returns>
        public List<Dictionary<string, object>> GetDevicesInfo()
        {
            List<Dictionary<string, object>> devicesInfo = new List<Dictionary<string, object>>();
            List<string> deviceIds = GetDevicesIds();

            foreach (string deviceId in deviceIds)
            {
                Dictionary<string, object> deviceProps = GetDeviceInfo(deviceId);
                
                
                devicesInfo.Add(deviceProps);
            }

            return devicesInfo;
        }

        public Dictionary<string, object> GetDeviceInfo(string deviceId)
        {
            List<string> deviceIds = GetDevicesIds();
            if (deviceIds.Count == 0)
            {
                OnErrorOccurred("No devices connected", "devices");
                return null;
            }

            if (!deviceIds.Contains(deviceId))
            {
                OnErrorOccurred($"Device {deviceId} not found", "devices");
                return null;
            }

            Dictionary<string, object> deviceProps = new Dictionary<string, object>();

            deviceProps["id"] = deviceId;
            deviceProps["model"] = GetDeviceModel(deviceId);
            deviceProps["android_version"] = GetDeviceProp(deviceId, "ro.build.version.release");
            deviceProps["sdk_version"] = GetDeviceProp(deviceId, "ro.build.version.sdk");
            deviceProps["manufacturer"] = GetDeviceProp(deviceId, "ro.product.manufacturer");
            deviceProps["brand"] = GetDeviceProp(deviceId, "ro.product.brand");
            deviceProps["battery"] = GetBatteryLevel(deviceId).ToString() + "%";
            deviceProps["features"] = GetDeviceFeatures(deviceId);
            deviceProps["storage"] = GetDeviceStorage(deviceId);

            return deviceProps;
        }

        /// <summary>
        /// Gets the model name of a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <returns>Model name</returns>
        public string GetDeviceModel(string deviceId)
        {
            return GetDeviceProp(deviceId, "ro.product.model");
        }

        /// <summary>
        /// Gets a system property from a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="property">Property name</param>
        /// <returns>Property value</returns>
        public string GetDeviceProp(string deviceId, string property)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell getprop {property}");
                return output.Trim();
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to get property {property}", $"-s {deviceId} shell getprop {property}", ex);
                return string.Empty;
            }
        }

        public List<string> GetDeviceFeatures(string deviceId)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell getprop | grep -i feature");
                return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(line => line.Trim())
                            .Where(line => !string.IsNullOrEmpty(line))
                            .ToList();
            }
            catch (Exception ex)
            {
                OnErrorOccurred("Failed to get device features", $"-s {deviceId} shell getprop | grep -i feature", ex);
                return new List<string>();
            }
        }

        public record DeviceStorage(string Used, string Available, string Total);
        public DeviceStorage GetDeviceStorage(string deviceId)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell df /data");
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length > 1)
                {
                    string[] parts = lines[1].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 4 &&
                        long.TryParse(parts[1], out long usedBytes) &&
                        long.TryParse(parts[3], out long availBytes))
                    {
                        string used = FormatBytes(usedBytes);
                        string available = FormatBytes(availBytes);
                        string total = FormatBytes(usedBytes + availBytes);
                        return new DeviceStorage(used, available, total);
                    }
                }

                return new DeviceStorage("Unknown", "Unknown", "Unknown");
            }
            catch (Exception ex)
            {
                OnErrorOccurred("Failed to get device storage", $"-s {deviceId} shell df /data", ex);
                return new DeviceStorage("Unknown", "Unknown", "Unknown");
            }
        }

        private string FormatBytes(long bytes)
        {
            double gb = bytes / (1024.0 * 1024 * 1024);
            return $"{gb:F1} GB";
        }

        /// <summary>
        /// Gets the battery level of a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <returns>Battery level percentage (0-100)</returns>
        public int GetBatteryLevel(string deviceId)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell dumpsys battery | grep level");
                Match match = Regex.Match(output, @"level:\s*(\d+)");
                
                if (match.Success && int.TryParse(match.Groups[1].Value, out int level))
                {
                    return level;
                }
                
                return -1;
            }
            catch (Exception ex)
            {
                OnErrorOccurred("Failed to get battery level", $"-s {deviceId} shell dumpsys battery", ex);
                return -1;
            }
        }

        /// <summary>
        /// Connects to a device over TCP/IP
        /// </summary>
        /// <param name="ipAddress">IP address of the device</param>
        /// <param name="port">Port (default: 5555)</param>
        /// <returns>True if connected successfully</returns>
        public bool ConnectDevice(string ipAddress, int port = 5555)
        {
            try
            {
                string output = ExecuteAdbCommand($"connect {ipAddress}:{port}");
                return output.Contains("connected to");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to connect to device {ipAddress}:{port}", $"connect {ipAddress}:{port}", ex);
                return false;
            }
        }

        /// <summary>
        /// Disconnects from a device over TCP/IP
        /// </summary>
        /// <param name="ipAddress">IP address of the device</param>
        /// <param name="port">Port (default: 5555)</param>
        /// <returns>True if disconnected successfully</returns>
        public bool DisconnectDevice(string ipAddress, int port = 5555)
        {
            try
            {
                string output = ExecuteAdbCommand($"disconnect {ipAddress}:{port}");
                return output.Contains("disconnected");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to disconnect from device {ipAddress}:{port}", $"disconnect {ipAddress}:{port}", ex);
                return false;
            }
        }

        /// <summary>
        /// Disconnects from all TCP/IP devices
        /// </summary>
        /// <returns>True if disconnected successfully</returns>
        public bool DisconnectAllDevices()
        {
            try
            {
                string output = ExecuteAdbCommand("disconnect");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred("Failed to disconnect all devices", "disconnect", ex);
                return false;
            }
        }

        /// <summary>
        /// Reboots a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <returns>True if reboot command was sent successfully</returns>
        public bool RebootDevice(string deviceId)
        {
            try
            {
                ExecuteAdbCommand($"-s {deviceId} reboot");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to reboot device {deviceId}", $"-s {deviceId} reboot", ex);
                return false;
            }
        }

        /// <summary>
        /// Reboots a device into recovery mode
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <returns>True if reboot command was sent successfully</returns>
        public bool RebootToRecovery(string deviceId)
        {
            try
            {
                ExecuteAdbCommand($"-s {deviceId} reboot recovery");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to reboot device {deviceId} to recovery", $"-s {deviceId} reboot recovery", ex);
                return false;
            }
        }

        /// <summary>
        /// Reboots a device into bootloader mode
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <returns>True if reboot command was sent successfully</returns>
        public bool RebootToBootloader(string deviceId)
        {
            try
            {
                ExecuteAdbCommand($"-s {deviceId} reboot bootloader");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to reboot device {deviceId} to bootloader", $"-s {deviceId} reboot bootloader", ex);
                return false;
            }
        }

        #endregion

        #region Public Methods - File Management

        /// <summary>
        /// Pushes a file to a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="localPath">Local file path</param>
        /// <param name="remotePath">Remote path on device</param>
        /// <returns>True if file was pushed successfully</returns>
        public bool PushFile(string deviceId, string localPath, string remotePath)
        {
            if (!File.Exists(localPath))
            {
                OnErrorOccurred($"Local file not found: {localPath}", null);
                return false;
            }

            try
            {
                long fileSize = new FileInfo(localPath).Length;
                string fileName = Path.GetFileName(localPath);
                
                Process process = new Process();
                process.StartInfo.FileName = AdbPath;
                process.StartInfo.Arguments = $"-s {deviceId} push \"{localPath}\" \"{remotePath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                
                StringBuilder output = new StringBuilder();
                process.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        
                        // Try to extract progress information
                        Match match = Regex.Match(e.Data, @"\[(\d+)%\]");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int progress))
                        {
                            OnProgressChanged(progress, $"Pushing {fileName}");
                        }
                    }
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
                
                OnCommandExecuted($"-s {deviceId} push \"{localPath}\" \"{remotePath}\"", output.ToString());
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to push file {localPath} to {remotePath}", $"-s {deviceId} push \"{localPath}\" \"{remotePath}\"", ex);
                return false;
            }
        }

        /// <summary>
        /// Pulls a file from a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="remotePath">Remote path on device</param>
        /// <param name="localPath">Local file path</param>
        /// <returns>True if file was pulled successfully</returns>
        public bool PullFile(string deviceId, string remotePath, string localPath)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(localPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                Process process = new Process();
                process.StartInfo.FileName = AdbPath;
                process.StartInfo.Arguments = $"-s {deviceId} pull \"{remotePath}\" \"{localPath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                
                StringBuilder output = new StringBuilder();
                process.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        
                        // Try to extract progress information
                        Match match = Regex.Match(e.Data, @"\[(\d+)%\]");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int progress))
                        {
                            OnProgressChanged(progress, $"Pulling file from {remotePath}");
                        }
                    }
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
                
                OnCommandExecuted($"-s {deviceId} pull \"{remotePath}\" \"{localPath}\"", output.ToString());
                return process.ExitCode == 0 && File.Exists(localPath);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to pull file {remotePath} to {localPath}", $"-s {deviceId} pull \"{remotePath}\" \"{localPath}\"", ex);
                return false;
            }
        }

        /// <summary>
        /// Lists files in a directory on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="remotePath">Remote path on device</param>
        /// <returns>List of files</returns>
        public List<string> ListFiles(string deviceId, string remotePath)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell ls -la \"{remotePath}\"");
                return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(line => line.Trim())
                            .Where(line => !string.IsNullOrEmpty(line))
                            .ToList();
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to list files in {remotePath}", $"-s {deviceId} shell ls -la \"{remotePath}\"", ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// Creates a directory on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="remotePath">Remote path on device</param>
        /// <returns>True if directory was created successfully</returns>
        public bool CreateDirectory(string deviceId, string remotePath)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell mkdir -p \"{remotePath}\"");
                return string.IsNullOrEmpty(output); // Command succeeded if no error output
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to create directory {remotePath}", $"-s {deviceId} shell mkdir -p \"{remotePath}\"", ex);
                return false;
            }
        }

        /// <summary>
        /// Deletes a file or directory on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="remotePath">Remote path on device</param>
        /// <param name="recursive">Whether to delete directories recursively</param>
        /// <returns>True if file or directory was deleted successfully</returns>
        public bool DeleteFile(string deviceId, string remotePath, bool recursive = false)
        {
            try
            {
                string cmd = recursive ? 
                    $"-s {deviceId} shell rm -rf \"{remotePath}\"" : 
                    $"-s {deviceId} shell rm \"{remotePath}\"";
                
                string output = ExecuteAdbCommand(cmd);
                return string.IsNullOrEmpty(output); // Command succeeded if no error output
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to delete {remotePath}", $"-s {deviceId} shell rm {(recursive ? "-rf" : "")} \"{remotePath}\"", ex);
                return false;
            }
        }

        #endregion

        #region Public Methods - App Management

        /// <summary>
        /// Installs an APK on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="apkPath">Path to APK file</param>
        /// <param name="reinstall">Whether to reinstall if app already exists</param>
        /// <returns>True if APK was installed successfully</returns>
        public bool InstallApk(string deviceId, string apkPath, bool reinstall = true)
        {
            if (!File.Exists(apkPath))
            {
                OnErrorOccurred($"APK file not found: {apkPath}", null);
                return false;
            }

            try
            {
                string fileName = Path.GetFileName(apkPath);
                string args = $"-s {deviceId} install {(reinstall ? "-r" : "")} \"{apkPath}\"";
                
                Process process = new Process();
                process.StartInfo.FileName = AdbPath;
                process.StartInfo.Arguments = args;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                
                StringBuilder output = new StringBuilder();
                process.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        
                        // Try to extract progress information
                        if (e.Data.Contains("Performing Streamed Install"))
                        {
                            OnProgressChanged(10, $"Installing {fileName}");
                        }
                        else if (e.Data.Contains("pkg: /data/app"))
                        {
                            OnProgressChanged(75, $"Installing {fileName}");
                        }
                    }
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
                
                string fullOutput = output.ToString();
                OnCommandExecuted(args, fullOutput);
                
                // Extract package name from APK
                string packageName = GetPackageNameFromApk(apkPath);
                
                if (fullOutput.Contains("Success") || fullOutput.Contains("success"))
                {
                    OnProgressChanged(100, $"Installed {fileName}");
                    if (!string.IsNullOrEmpty(packageName))
                    {
                        OnPackageInstalled(deviceId, packageName);
                    }
                    return true;
                }
                else
                {
                    OnErrorOccurred($"Failed to install APK: {fullOutput}", args);
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to install APK {apkPath}", $"-s {deviceId} install {(reinstall ? "-r" : "")} \"{apkPath}\"", ex);
                return false;
            }
        }

        /// <summary>
        /// Uninstalls an app from a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="packageName">Package name</param>
        /// <param name="keepData">Whether to keep app data and cache</param>
        /// <returns>True if app was uninstalled successfully</returns>
        public bool UninstallApp(string deviceId, string packageName, bool keepData = false)
        {
            try
            {
                string args = $"-s {deviceId} uninstall {(keepData ? "-k" : "")} {packageName}";
                string output = ExecuteAdbCommand(args);
                
                if (output.Contains("Success") || output.Contains("success"))
                {
                    OnPackageUninstalled(deviceId, packageName);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to uninstall app {packageName}", $"-s {deviceId} uninstall {(keepData ? "-k" : "")} {packageName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Copies a folder from a device to the local machine
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="remotePath">Remote path on device</param>
        /// <param name="localPath">Local path on local machine</param>
        /// <returns>True if folder was copied successfully</returns>
        public bool CopyFolder(string deviceId, string remotePath, string localPath)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(localPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                string output = ExecuteAdbCommand($"-s {deviceId} shell cp -r \"{remotePath}\" \"{localPath}\"");
                return string.IsNullOrEmpty(output); // Command succeeded if no error output
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to copy folder {remotePath} to {localPath}", $"-s {deviceId} shell cp -r \"{remotePath}\" \"{localPath}\"", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets a list of installed packages on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="systemApps">Whether to include system apps</param>
        /// <returns>List of package names</returns>
        public List<string> GetInstalledPackages(string deviceId, bool systemApps = false)
        {
            try
            {
                string cmd = systemApps ? 
                    $"-s {deviceId} shell pm list packages" : 
                    $"-s {deviceId} shell pm list packages -3";
                
                string output = ExecuteAdbCommand(cmd);
                
                return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(line => line.Replace("package:", "").Trim())
                            .Where(pkg => !string.IsNullOrEmpty(pkg))
                            .ToList();
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to get installed packages", $"-s {deviceId} shell pm list packages", ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// Launches an app on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="packageName">Package name</param>
        /// <param name="activityName">Activity name (optional)</param>
        /// <returns>True if app was launched successfully</returns>
        public bool LaunchApp(string deviceId, string packageName, string activityName = null)
        {
            try
            {
                string cmd;
                
                if (string.IsNullOrEmpty(activityName))
                {
                    // Try to get the main activity
                    string mainActivity = GetMainActivity(deviceId, packageName);
                    
                    cmd = string.IsNullOrEmpty(mainActivity) ?
                        $"-s {deviceId} shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1" :
                        $"-s {deviceId} shell am start -n {packageName}/{mainActivity}";
                }
                else
                {
                    cmd = $"-s {deviceId} shell am start -n {packageName}/{activityName}";
                }
                
                string output = ExecuteAdbCommand(cmd);
                return output.Contains("Starting") || output.Contains("Events injected");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to launch app {packageName}", $"-s {deviceId} shell am start -n {packageName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Stops an app on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="packageName">Package name</param>
        /// <returns>True if app was stopped successfully</returns>
        public bool StopApp(string deviceId, string packageName)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell am force-stop {packageName}");
                return string.IsNullOrEmpty(output); // Command succeeded if no error output
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to stop app {packageName}", $"-s {deviceId} shell am force-stop {packageName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Clears app data on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="packageName">Package name</param>
        /// <returns>True if app data was cleared successfully</returns>
        public bool ClearAppData(string deviceId, string packageName)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell pm clear {packageName}");
                return output.Contains("Success") || output.Contains("success");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to clear app data for {packageName}", $"-s {deviceId} shell pm clear {packageName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the version of an app on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="packageName">Package name</param>
        /// <returns>Version name and code, or empty string if not found</returns>
        public (string VersionName, string VersionCode) GetAppVersion(string deviceId, string packageName)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell dumpsys package {packageName} | grep version");
                
                string versionName = string.Empty;
                string versionCode = string.Empty;
                
                Match nameMatch = Regex.Match(output, @"versionName=([^\s]+)");
                if (nameMatch.Success)
                {
                    versionName = nameMatch.Groups[1].Value;
                }
                
                Match codeMatch = Regex.Match(output, @"versionCode=(\d+)");
                if (codeMatch.Success)
                {
                    versionCode = codeMatch.Groups[1].Value;
                }
                
                return (versionName, versionCode);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to get app version for {packageName}", $"-s {deviceId} shell dumpsys package {packageName}", ex);
                return (string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// Gets the main activity of an app
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="packageName">Package name</param>
        /// <returns>Main activity name, or empty string if not found</returns>
        public string GetMainActivity(string deviceId, string packageName)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell dumpsys package {packageName} | grep -A 5 \"MAIN\"");
                
                Match match = Regex.Match(output, packageName + @"/([^\s]+)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to get main activity for {packageName}", $"-s {deviceId} shell dumpsys package {packageName}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the package name from an APK file
        /// </summary>
        /// <param name="apkPath">Path to APK file</param>
        /// <returns>Package name, or empty string if not found</returns>
        public string GetPackageNameFromApk(string apkPath)
        {
            try
            {
                string output = ExecuteCommand("aapt", $"dump badging \"{apkPath}\" | grep package");
                
                Match match = Regex.Match(output, @"package: name='([^']+)'");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                
                return string.Empty;
            }
            catch (Exception)
            {
                // Try alternative with ADB
                try
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    Directory.CreateDirectory(tempDir);
                    
                    string tempApk = Path.Combine(tempDir, "temp.apk");
                    File.Copy(apkPath, tempApk);
                    
                    string output = ExecuteAdbCommand($"shell pm dump {tempApk} | grep package");
                    Directory.Delete(tempDir, true);
                    
                    Match match = Regex.Match(output, @"package: name='([^']+)'");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
                catch { }
                
                return string.Empty;
            }
        }

        #endregion

        #region Public Methods - Shell Commands

        /// <summary>
        /// Executes a shell command on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="command">Shell command</param>
        /// <returns>Command output</returns>
        public string ExecuteShellCommand(string deviceId, string command)
        {
            return ExecuteAdbCommand($"-s {deviceId} shell {command}");
        }

        /// <summary>
        /// Takes a screenshot from a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="savePath">Path to save the screenshot</param>
        /// <returns>True if screenshot was taken successfully</returns>
        public bool TakeScreenshot(string deviceId, string savePath)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                string remotePath = "/sdcard/screenshot.png";
                
                // Take screenshot
                ExecuteAdbCommand($"-s {deviceId} shell screencap -p {remotePath}");
                
                // Pull the file
                bool result = PullFile(deviceId, remotePath, savePath);
                
                // Clean up
                ExecuteAdbCommand($"-s {deviceId} shell rm {remotePath}");
                
                return result && File.Exists(savePath);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to take screenshot", $"-s {deviceId} shell screencap", ex);
                return false;
            }
        }

        /// <summary>
        /// Records the screen of a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="savePath">Path to save the recording</param>
        /// <param name="timeLimit">Time limit in seconds (0 = no limit)</param>
        /// <param name="bitRate">Bit rate in Mbps</param>
        /// <returns>True if screen was recorded successfully</returns>
        public bool RecordScreen(string deviceId, string savePath, int timeLimit = 180, int bitRate = 4)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                string remotePath = "/sdcard/screenrecord.mp4";
                string timeLimitParam = timeLimit > 0 ? $"--time-limit {timeLimit}" : "";
                
                // Start recording in a separate process
                Process process = new Process();
                process.StartInfo.FileName = AdbPath;
                process.StartInfo.Arguments = $"-s {deviceId} shell screenrecord {timeLimitParam} --bit-rate {bitRate}M {remotePath}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                
                process.Start();
                
                // Show progress for fixed time recordings
                if (timeLimit > 0)
                {
                    Task.Run(async () =>
                    {
                        for (int i = 1; i <= timeLimit; i++)
                        {
                            int percentage = (int)((double)i / timeLimit * 100);
                            OnProgressChanged(percentage, "Recording screen");
                            await Task.Delay(1000);
                            
                            if (process.HasExited)
                                break;
                        }
                    });
                }
                else
                {
                    // For unlimited recordings, just show that recording started
                    OnProgressChanged(0, "Recording screen");
                }
                
                process.WaitForExit();
                
                // Pull the file
                bool result = PullFile(deviceId, remotePath, savePath);
                
                // Clean up
                ExecuteAdbCommand($"-s {deviceId} shell rm {remotePath}");
                
                if (result && File.Exists(savePath))
                {
                    OnProgressChanged(100, "Screen recording completed");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to record screen", $"-s {deviceId} shell screenrecord", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets device logs
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="filter">Log filter</param>
        /// <param name="limit">Maximum number of lines (0 = no limit)</param>
        /// <returns>Log output</returns>
        public string GetLogs(string deviceId, string filter = "", int limit = 1000)
        {
            try
            {
                string limitParam = limit > 0 ? $"-T {limit}" : "";
                string filterParam = !string.IsNullOrEmpty(filter) ? $"| grep \"{filter}\"" : "";
                
                return ExecuteAdbCommand($"-s {deviceId} shell logcat -d {limitParam} {filterParam}");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to get device logs", $"-s {deviceId} shell logcat", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Clears device logs
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <returns>True if logs were cleared successfully</returns>
        public bool ClearLogs(string deviceId)
        {
            try
            {
                ExecuteAdbCommand($"-s {deviceId} shell logcat -c");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to clear device logs", $"-s {deviceId} shell logcat -c", ex);
                return false;
            }
        }

        /// <summary>
        /// Sends keyevent to a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="keycode">Keycode</param>
        /// <returns>True if keyevent was sent successfully</returns>
        public bool SendKeyEvent(string deviceId, int keycode)
        {
            try
            {
                ExecuteAdbCommand($"-s {deviceId} shell input keyevent {keycode}");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to send keyevent {keycode}", $"-s {deviceId} shell input keyevent {keycode}", ex);
                return false;
            }
        }

        /// <summary>
        /// Sends text to a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="text">Text to send</param>
        /// <returns>True if text was sent successfully</returns>
        public bool SendText(string deviceId, string text)
        {
            try
            {
                // Replace spaces with %s
                string escapedText = text.Replace(" ", "%s");
                ExecuteAdbCommand($"-s {deviceId} shell input text \"{escapedText}\"");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to send text", $"-s {deviceId} shell input text", ex);
                return false;
            }
        }

        /// <summary>
        /// Taps on the screen of a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>True if tap was sent successfully</returns>
        public bool Tap(string deviceId, int x, int y)
        {
            try
            {
                ExecuteAdbCommand($"-s {deviceId} shell input tap {x} {y}");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to send tap at ({x}, {y})", $"-s {deviceId} shell input tap {x} {y}", ex);
                return false;
            }
        }

        /// <summary>
        /// Swipes on the screen of a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="x1">Start X coordinate</param>
        /// <param name="y1">Start Y coordinate</param>
        /// <param name="x2">End X coordinate</param>
        /// <param name="y2">End Y coordinate</param>
        /// <param name="duration">Duration in milliseconds</param>
        /// <returns>True if swipe was sent successfully</returns>
        public bool Swipe(string deviceId, int x1, int y1, int x2, int y2, int duration = 300)
        {
            try
            {
                ExecuteAdbCommand($"-s {deviceId} shell input swipe {x1} {y1} {x2} {y2} {duration}");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to send swipe from ({x1}, {y1}) to ({x2}, {y2})", 
                    $"-s {deviceId} shell input swipe {x1} {y1} {x2} {y2} {duration}", ex);
                return false;
            }
        }

        #endregion

        #region Public Methods - Network


        public async Task<bool> InstallAPKWeb(string deviceId, string apkPath)
        {
            if (apkPath.Contains(AppPath.RCLONE_MOUNT_DIR)) {
                string FileName = apkPath.Split('/').Last();
                string FilePath = apkPath.Replace(AppPath.RCLONE_MOUNT_DIR, AppPath.TMP_DIR);
                Console.WriteLine("Remote Install");
                await FileManager.CopyFile(apkPath, FilePath, "");
                FileManager.FileCopyProgress += (id, src, dest, current, total, percent) => {
                    Console.WriteLine($"Copy progress: {percent}% - {current}/{total} bytes");
                };
            }
            return true;
        }

        /// <summary>
        /// Gets the IP address of a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <returns>IP address, or empty string if not found</returns>
        public string GetDeviceIpAddress(string deviceId)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} shell ip addr show wlan0");
                
                Match match = Regex.Match(output, @"inet\s+(\d+\.\d+\.\d+\.\d+)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                
                // Try alternative command
                output = ExecuteAdbCommand($"-s {deviceId} shell ifconfig wlan0");
                match = Regex.Match(output, @"inet addr:(\d+\.\d+\.\d+\.\d+)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to get device IP address", $"-s {deviceId} shell ip addr show wlan0", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Enables/disables WiFi on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="enable">Whether to enable WiFi</param>
        /// <returns>True if WiFi state was changed successfully</returns>
        public bool SetWifi(string deviceId, bool enable)
        {
            try
            {
                ExecuteAdbCommand($"-s {deviceId} shell svc wifi {(enable ? "enable" : "disable")}");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to {(enable ? "enable" : "disable")} WiFi", 
                    $"-s {deviceId} shell svc wifi {(enable ? "enable" : "disable")}", ex);
                return false;
            }
        }

        /// <summary>
        /// Enables/disables mobile data on a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="enable">Whether to enable mobile data</param>
        /// <returns>True if mobile data state was changed successfully</returns>
        public bool SetMobileData(string deviceId, bool enable)
        {
            try
            {
                ExecuteAdbCommand($"-s {deviceId} shell svc data {(enable ? "enable" : "disable")}");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to {(enable ? "enable" : "disable")} mobile data", 
                    $"-s {deviceId} shell svc data {(enable ? "enable" : "disable")}", ex);
                return false;
            }
        }

        /// <summary>
        /// Sets up port forwarding
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="localPort">Local port</param>
        /// <param name="devicePort">Device port</param>
        /// <returns>True if port forwarding was set up successfully</returns>
        public bool ForwardPort(string deviceId, int localPort, int devicePort)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} forward tcp:{localPort} tcp:{devicePort}");
                return string.IsNullOrEmpty(output); // Command succeeded if no error output
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to set up port forwarding", $"-s {deviceId} forward tcp:{localPort} tcp:{devicePort}", ex);
                return false;
            }
        }

        /// <summary>
        /// Removes port forwarding
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="localPort">Local port</param>
        /// <returns>True if port forwarding was removed successfully</returns>
        public bool RemovePortForward(string deviceId, int localPort)
        {
            try
            {
                string output = ExecuteAdbCommand($"-s {deviceId} forward --remove tcp:{localPort}");
                return string.IsNullOrEmpty(output); // Command succeeded if no error output
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to remove port forwarding", $"-s {deviceId} forward --remove tcp:{localPort}", ex);
                return false;
            }
        }

        /// <summary>
        /// Removes all port forwardings
        /// </summary>
        /// <returns>True if all port forwardings were removed successfully</returns>
        public bool RemoveAllPortForwards()
        {
            try
            {
                string output = ExecuteAdbCommand("forward --remove-all");
                return string.IsNullOrEmpty(output); // Command succeeded if no error output
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to remove all port forwardings", "forward --remove-all", ex);
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Executes an ADB command
        /// </summary>
        /// <param name="command">Command</param>
        /// <returns>Command output</returns>
        private string ExecuteAdbCommand(string command)
        {
            OnCommandExecuting(command);
            
            using (Process process = new Process())
            {
                process.StartInfo.FileName = AdbPath;
                process.StartInfo.Arguments = command;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                try
                {
                    process.Start();
                    
                    // Use timeout for command execution
                    if (!process.WaitForExit(CommandTimeout))
                    {
                        process.Kill();
                        throw new TimeoutException($"ADB command timed out after {CommandTimeout}ms: {command}");
                    }
                    
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    string fullOutput = output + error;
                    
                    OnCommandExecuted(command, fullOutput);
                    
                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"ADB command failed with exit code {process.ExitCode}: {error}");
                    }
                    
                    return fullOutput;
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Error executing ADB command: {command}", command, ex);
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Executes a generic command
        /// </summary>
        /// <param name="command">Command</param>
        /// <param name="arguments">Arguments</param>
        /// <returns>Command output</returns>
        private string ExecuteCommand(string command, string arguments)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = command;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                try
                {
                    process.Start();
                    
                    if (!process.WaitForExit(CommandTimeout))
                    {
                        process.Kill();
                        throw new TimeoutException($"Command timed out after {CommandTimeout}ms: {command} {arguments}");
                    }
                    
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Command failed with exit code {process.ExitCode}: {error}");
                    }
                    
                    return output + error;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error executing command: {command} {arguments}", ex);
                }
            }
        }

        #endregion

        #region Event Methods

        /// <summary>
        /// Raises the DeviceConnected event
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="deviceModel">Device model</param>
        protected virtual void OnDeviceConnected(string deviceId, Dictionary<string, object> deviceInfo)
        {
            DeviceConnected?.Invoke(this, new DeviceStatusChangedEventArgs(deviceId, deviceInfo));
        }

        /// <summary>
        /// Raises the DeviceDisconnected event
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="deviceModel">Device model</param>
        protected virtual void OnDeviceDisconnected(string deviceId, Dictionary<string, object> deviceInfo)
        {
            DeviceDisconnected?.Invoke(this, new DeviceStatusChangedEventArgs(deviceId, deviceInfo));
        }

        /// <summary>
        /// Raises the PackageInstalled event
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="packageName">Package name</param>
        protected virtual void OnPackageInstalled(string deviceId, string packageName)
        {
            PackageInstalled?.Invoke(this, new PackageStatusEventArgs(deviceId, packageName));
        }

        /// <summary>
        /// Raises the PackageUninstalled event
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="packageName">Package name</param>
        protected virtual void OnPackageUninstalled(string deviceId, string packageName)
        {
            PackageUninstalled?.Invoke(this, new PackageStatusEventArgs(deviceId, packageName));
        }

        /// <summary>
        /// Raises the ProgressChanged event
        /// </summary>
        /// <param name="percentage">Progress percentage</param>
        /// <param name="message">Progress message</param>
        protected virtual void OnProgressChanged(int percentage, string message)
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs(percentage, message));
        }

        /// <summary>
        /// Raises the ErrorOccurred event
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="command">Command</param>
        /// <param name="exception">Exception</param>
        protected virtual void OnErrorOccurred(string errorMessage, string command, Exception exception = null)
        {
            ErrorOccurred?.Invoke(this, new AdbErrorEventArgs(errorMessage, command, exception));
        }

        /// <summary>
        /// Raises the CommandExecuting event
        /// </summary>
        /// <param name="command">Command</param>
        protected virtual void OnCommandExecuting(string command)
        {
            CommandExecuting?.Invoke(this, new AdbCommandEventArgs(command));
        }

        /// <summary>
        /// Raises the CommandExecuted event
        /// </summary>
        /// <param name="command">Command</param>
        /// <param name="output">Command output</param>
        protected virtual void OnCommandExecuted(string command, string output)
        {
            CommandExecuted?.Invoke(this, new AdbCommandEventArgs(command, output));
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the ADB wrapper
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the ADB wrapper
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopMonitoringDevices();
                    _monitorCts?.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}