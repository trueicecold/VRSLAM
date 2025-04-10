using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRSLAM.Libs;

namespace VRSLAM.Managers
{
    public class FileManager
    {
        public delegate void FileCopyProgressEventHandler(string operationId, string sourcePath, string destinationPath, long bytesProcessed, long totalBytes, double percentage);
        public delegate void FileCopyCompletionEventHandler(string operationId, string sourcePath, string destinationPath, bool success, string errorMessage);
        
        public static event FileCopyProgressEventHandler FileCopyProgress;
        public static event FileCopyCompletionEventHandler FileCopyCompleted;
        
        public static void InitHandlers()
        {
            Shared.Window.RegisterWebMessageReceivedHandler((object sender, string messageStr) =>
            {
                dynamic message = JSON.Parse(messageStr);

                switch (message.action.ToString())
                {
                    case "list_files":
                        if (Helpers.HasProperty(message, "mount")) {
                            ListFiles(AppPath.RCLONE_MOUNT_DIR);
                        }
                        else if (Helpers.HasProperty(message, "home")) {
                            ListFiles(AppPath.HOME_DIR);
                        }
                        else {
                            ListFiles(message.path.ToString());
                        }
                        break;
                    default:
                        break;
                }
            });
        }

        private static void ListFiles(string path)
        {
            string[] files = System.IO.Directory.GetFiles(path);
            string[] directories = System.IO.Directory.GetDirectories(path);

            Shared.Window.SendWebMessage(JSON.Stringify(new
            {
                type = "list_files",
                files = files,
                directories = directories
            }));

            //sort directories and files
            Array.Sort(directories, StringComparer.InvariantCulture);
            Array.Sort(files, StringComparer.InvariantCulture);
            
            directories = directories.Select(d => d.Replace(path + "/", "")).ToArray();
            files = files.Select(f => f.Replace(path + "/", "")).ToArray();

            //send sorted directories and files
            Shared.Window.SendWebMessage(JSON.Stringify(new
            {
                type = "list_files",
                files = files,
                directories = directories,
                path = path
            }));
        }

        /// <summary>
        /// Copies a file from source to destination with progress reporting
        /// </summary>
        /// <param name="sourcePath">The source file path</param>
        /// <param name="destinationPath">The destination file path</param>
        /// <param name="operationId">Unique ID to track this copy operation</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public static async Task CopyFile(string sourcePath, string destinationPath, string operationId)
        {
            try
            {
                using (var sourceStream = File.OpenRead(sourcePath))
                using (var destinationStream = File.Create(destinationPath))
                {
                    long totalBytes = sourceStream.Length;
                    long bytesRead = 0;
                    byte[] buffer = new byte[81920]; // 80KB buffer for better performance
                    int bytesReadThisTime;
                    
                    // Report initial progress
                    OnFileCopyProgress(operationId, sourcePath, destinationPath, bytesRead, totalBytes, 0);
                    
                    while ((bytesReadThisTime = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await destinationStream.WriteAsync(buffer, 0, bytesReadThisTime);
                        bytesRead += bytesReadThisTime;
                        
                        // Report progress (about every 1% or when complete)
                        if (totalBytes > 0 && (bytesRead % (Math.Max(totalBytes / 100, 1)) == 0 || bytesRead == totalBytes))
                        {
                            double percentage = totalBytes > 0 ? (double)bytesRead / totalBytes * 100 : 100;
                            OnFileCopyProgress(operationId, sourcePath, destinationPath, bytesRead, totalBytes, Math.Round(percentage, 2));
                        }
                    }
                    
                    // Report completion
                    OnFileCopyCompleted(operationId, sourcePath, destinationPath, true, null);
                }
            }
            catch (Exception ex)
            {
                // Report error
                OnFileCopyCompleted(operationId, sourcePath, destinationPath, false, ex.Message);
            }
        }
        
        // Helper methods to raise events safely
        private static void OnFileCopyProgress(string operationId, string sourcePath, string destinationPath, 
                                         long bytesProcessed, long totalBytes, double percentage)
        {
            FileCopyProgress?.Invoke(operationId, sourcePath, destinationPath, bytesProcessed, totalBytes, percentage);
        }
        
        private static void OnFileCopyCompleted(string operationId, string sourcePath, string destinationPath, 
                                          bool success, string errorMessage)
        {
            FileCopyCompleted?.Invoke(operationId, sourcePath, destinationPath, success, errorMessage);
        }
    }
}