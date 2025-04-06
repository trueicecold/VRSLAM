using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace VRSLAM.Libs
{
    public class RClone
    {
        private readonly string _configPath;
        private Process _mountProcess;

        /// <summary>
        /// Initializes a new instance of the RClone wrapper.
        /// </summary>
        /// <param name="rclonePath">Path to the rclone executable</param>
        /// <param name="configPath">Path to the rclone config file</param>
        public RClone(string configPath = null)
        {
            _configPath = configPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "rclone", 
                "rclone.conf");
        }

        /// <summary>
        /// Mounts a remote filesystem to a local directory.
        /// </summary>
        /// <param name="remoteName">The name of the remote in the config file</param>
        /// <param name="remotePath">Optional path within the remote</param>
        /// <param name="mountPoint">Local directory where the remote will be mounted</param>
        /// <param name="additionalOptions">Additional rclone mount options</param>
        /// <returns>True if the mount was successful</returns>
        public bool Mount(string remoteName, string remotePath, string mountPoint, string additionalOptions = "")
        {
            if (string.IsNullOrEmpty(remoteName))
                throw new ArgumentException("Remote name cannot be empty", nameof(remoteName));

            if (string.IsNullOrEmpty(mountPoint))
                throw new ArgumentException("Mount point cannot be empty", nameof(mountPoint));

            // Create mount point directory if it doesn't exist
            if (!Directory.Exists(mountPoint))
            {
                Directory.CreateDirectory(mountPoint);
            }

            // Build remote string
            string remote = $"{remoteName}:";
            if (!string.IsNullOrEmpty(remotePath))
            {
                remote += remotePath;
            }

            // Build command arguments
            string arguments = $"mount {remote} \"{mountPoint}\" --config \"{_configPath}\" {additionalOptions}";

            try
            {
                // Start rclone process
                _mountProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = AppPath.RCLONE_PATH,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                _mountProcess.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"RClone: {e.Data}");
                };

                _mountProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"RClone Error: {e.Data}");
                };

                _mountProcess.Start();
                _mountProcess.BeginOutputReadLine();
                _mountProcess.BeginErrorReadLine();

                // Give some time for mount to initialize
                Task.Delay(1000).Wait();
                
                // Check if process is still running
                if (_mountProcess.HasExited)
                {
                    Console.WriteLine($"RClone exited with code {_mountProcess.ExitCode}");
                    return false;
                }

                Console.WriteLine($"Successfully mounted {remote} to {mountPoint}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to mount: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Kills the rclone mount process if it is running.
        /// </summary>
        /// <returns>True if the process was killed successfully</returns>
        /// <remarks>
        /// This method is a fallback to ensure that the mount process is terminated.
        /// It is not intended to be used for unmounting the filesystem.
        /// </remarks>
        public bool Kill()
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = (AppPath.IS_WINDOWS_32 || AppPath.IS_WINDOWS_64) ? "taskkill" : "killall";
                process.StartInfo.Arguments = (AppPath.IS_WINDOWS_32 || AppPath.IS_WINDOWS_64) ? "/F /T /IM rclone.exe" : "-9 rclone";

                try
                {
                    process.Start();
                    process.WaitForExit();
                    Console.WriteLine("RClone mount process killed successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to kill RClone: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Unmounts a previously mounted filesystem.
        /// </summary>
        /// <param name="mountPoint">The mount point to unmount</param>
        /// <returns>True if the unmount was successful</returns>
        public bool Unmount(string mountPoint)
        {
            using (var process = new Process())
            {
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                if (AppPath.IS_MAC_64 || AppPath.IS_MAC_ARM64 || AppPath.IS_LINUX_32 || AppPath.IS_LINUX_64)
                {
                    process.StartInfo.FileName = "umount";
                    process.StartInfo.Arguments = "\"" + mountPoint + "\"";
                }
                else if (AppPath.IS_WINDOWS_32 || AppPath.IS_WINDOWS_64)
                {
                    process.StartInfo.FileName = "fusermount";
                    process.StartInfo.Arguments = $"-uz \"{mountPoint}\"";
                }

                try
                {
                    process.Start();
                    process.WaitForExit();
                    Console.WriteLine("RClone unmounted successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to unmount RClone: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Synchronizes files from source to destination.
        /// </summary>
        /// <param name="source">Source directory or remote</param>
        /// <param name="destination">Destination directory or remote</param>
        /// <param name="syncOptions">Additional rclone sync options</param>
        /// <param name="waitForCompletion">Whether to wait for sync to complete before returning</param>
        /// <returns>A Task representing the sync operation with the exit code</returns>
        public async Task<int> SyncAsync(string source, string destination, string syncOptions = "", bool waitForCompletion = true)
        {
            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("Source cannot be empty", nameof(source));

            if (string.IsNullOrEmpty(destination))
                throw new ArgumentException("Destination cannot be empty", nameof(destination));

            // Build command arguments
            string arguments = $"sync \"{source}:\" \"{destination}\" --config \"{_configPath}\" {syncOptions}";

            try
            {
                Console.WriteLine(AppPath.RCLONE_PATH + " " + arguments);
                // Start rclone process
                using (var syncProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = AppPath.RCLONE_PATH,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                })
                {
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();

                    syncProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Console.WriteLine($"RClone Sync: {e.Data}");
                            outputBuilder.AppendLine(e.Data);
                        }
                    };

                    syncProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Console.WriteLine($"RClone Sync Error: {e.Data}");
                            errorBuilder.AppendLine(e.Data);
                        }
                    };

                    syncProcess.Start();
                    syncProcess.BeginOutputReadLine();
                    syncProcess.BeginErrorReadLine();

                    if (waitForCompletion)
                    {
                        await syncProcess.WaitForExitAsync();
                        Console.WriteLine($"Sync completed with exit code {syncProcess.ExitCode}");
                        return syncProcess.ExitCode;
                    }
                    else
                    {
                        return 0; // Return 0 immediately since we're not waiting
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to sync: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Synchronizes files from source to destination (synchronous version).
        /// </summary>
        /// <param name="source">Source directory or remote</param>
        /// <param name="destination">Destination directory or remote</param>
        /// <param name="syncOptions">Additional rclone sync options</param>
        /// <returns>The exit code of the sync operation</returns>
        public int Sync(string source, string destination, string syncOptions = "")
        {
            return SyncAsync(source, destination, syncOptions, true).GetAwaiter().GetResult();
        }
    }
}