using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace VRSLAM.Libs
{
    public class FileDownloadProgressChangedEventArgs : EventArgs
    {
        public int ProgressPercentage { get; }
        public long BytesReceived { get; }
        public long TotalBytesToReceive { get; }
        public long BytesTransferred { get; }

        public FileDownloadProgressChangedEventArgs(int progressPercentage, long bytesTransferred, long bytesReceived, long totalBytesToReceive)
        {
            ProgressPercentage = progressPercentage;
            BytesTransferred = bytesTransferred;
            BytesReceived = bytesReceived;
            TotalBytesToReceive = totalBytesToReceive;
        }
    }

    public class FileDownloadErrorEventArgs : EventArgs
    {
        public Exception Error { get; }
        public string FilePath { get; }
        public string Url { get; }
        public long BytesReceived { get; }
        public HttpStatusCode? StatusCode { get; }

        public FileDownloadErrorEventArgs(Exception error, string filePath, string url, long bytesReceived, HttpStatusCode? statusCode = null)
        {
            Error = error;
            FilePath = filePath;
            Url = url;
            BytesReceived = bytesReceived;
            StatusCode = statusCode;
        }
    }

    public class FileDownloadCompletedEventArgs : EventArgs
    {
        public bool Success { get; }
        public string FilePath { get; }
        public Exception Error { get; }
        public long TotalBytesDownloaded { get; }
        public TimeSpan Duration { get; }
        public bool Cancelled { get; }

        public FileDownloadCompletedEventArgs(bool success, string filePath, Exception error, 
            long totalBytesDownloaded, TimeSpan duration, bool cancelled = false)
        {
            Success = success;
            FilePath = filePath;
            Error = error;
            TotalBytesDownloaded = totalBytesDownloaded;
            Duration = duration;
            Cancelled = cancelled;
        }
    }

    public class FileDownloadResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public Exception Error { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Cancelled { get; set; }
        public HttpStatusCode? StatusCode { get; set; }
    }

    public class FileDownloader
    {
        public event EventHandler<FileDownloadProgressChangedEventArgs> ProgressChanged;
        public event EventHandler<FileDownloadCompletedEventArgs> DownloadCompleted;
        public event EventHandler<FileDownloadErrorEventArgs> Error;
        
        /// <summary>
        /// Downloads a file with progress reporting using HttpClient
        /// </summary>
        /// <param name="url">The URL of the file to download</param>
        /// <param name="destinationPath">The local path where the file will be saved</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Result of the download operation</returns>
        public async Task<FileDownloadResult> DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            long totalBytesRead = 0;
            bool cancelled = false;
            bool errorOccurred = false;
            HttpStatusCode? statusCode = null;

            FileDownloadResult result = new FileDownloadResult
            {
                FilePath = destinationPath,
                Success = false
            };

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Set timeout if needed
                    client.Timeout = TimeSpan.FromMinutes(30);
                    
                    // Make HEAD request to get file size first (if server supports it)
                    long? totalBytes = null;
                    try
                    {
                        using (var headRequest = new HttpRequestMessage(HttpMethod.Head, url))
                        using (var headResponse = await client.SendAsync(headRequest, cancellationToken))
                        {
                            statusCode = headResponse.StatusCode;
                            
                            if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
                            {
                                totalBytes = headResponse.Content.Headers.ContentLength.Value;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors from HEAD request, we'll continue with GET
                    }

                    // Send GET request to download the file
                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        statusCode = response.StatusCode;
                        response.EnsureSuccessStatusCode();

                        // If we couldn't get the size from HEAD, try to get it from the GET response
                        if (!totalBytes.HasValue && response.Content.Headers.ContentLength.HasValue)
                        {
                            totalBytes = response.Content.Headers.ContentLength.Value;
                        }

                        // Create directory if it doesn't exist
                        string directory = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // Open file for writing
                        using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            {
                                // Download buffer
                                byte[] buffer = new byte[8192]; // 8KB buffer
                                int bytesRead;

                                // Read and write in chunks
                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                    
                                    // Update progress
                                    totalBytesRead += bytesRead;
                                    
                                    // Report progress if we know the total size
                                    if (totalBytes.HasValue)
                                    {
                                        int progressPercentage = (int)((totalBytesRead * 100) / totalBytes.Value);
                                        OnProgressChanged(new FileDownloadProgressChangedEventArgs(
                                            progressPercentage,
                                            bytesRead,
                                            totalBytesRead,
                                            totalBytes.Value));
                                    }
                                    else
                                    {
                                        // Report progress without percentage if total is unknown
                                        OnProgressChanged(new FileDownloadProgressChangedEventArgs(
                                            -1,
                                            bytesRead,
                                            totalBytesRead,
                                            -1));
                                    }
                                }
                            }
                        }

                        // Verify the file exists after download
                        if (File.Exists(destinationPath))
                        {
                            result.Success = true;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                errorOccurred = true;
                result.Error = new OperationCanceledException("Download was cancelled");
                result.StatusCode = statusCode;
                
                // Delete partial file if it exists
                TryDeleteFile(destinationPath);
                
                // Raise error event
                OnError(new FileDownloadErrorEventArgs(result.Error, destinationPath, url, totalBytesRead, statusCode));
            }
            catch (HttpRequestException ex)
            {
                errorOccurred = true;
                result.Error = ex;
                result.StatusCode = statusCode;
                
                // Delete partial file if it exists
                TryDeleteFile(destinationPath);
                
                // Raise error event with status code
                OnError(new FileDownloadErrorEventArgs(ex, destinationPath, url, totalBytesRead, statusCode));
            }
            catch (Exception ex)
            {
                errorOccurred = true;
                result.Error = ex;
                result.StatusCode = statusCode;
                
                // Delete partial file if it exists
                TryDeleteFile(destinationPath);
                
                // Raise error event
                OnError(new FileDownloadErrorEventArgs(ex, destinationPath, url, totalBytesRead, statusCode));
            }
            finally
            {
                var downloadTime = DateTime.Now - startTime;
                
                // Update result with additional information
                result.TotalBytesDownloaded = totalBytesRead;
                result.Duration = downloadTime;
                result.Cancelled = cancelled;
                
                // Only raise completed event if no error occurred
                if (!errorOccurred)
                {
                    // Raise completed event
                    OnDownloadCompleted(new FileDownloadCompletedEventArgs(
                        result.Success, 
                        destinationPath, 
                        result.Error, 
                        totalBytesRead, 
                        downloadTime,
                        cancelled));
                }
            }

            return result;
        }

        protected virtual void OnProgressChanged(FileDownloadProgressChangedEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }
        
        protected virtual void OnDownloadCompleted(FileDownloadCompletedEventArgs e)
        {
            DownloadCompleted?.Invoke(this, e);
        }
        
        protected virtual void OnError(FileDownloadErrorEventArgs e)
        {
            Error?.Invoke(this, e);
        }
        
        private void TryDeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }

        /// <summary>
        /// Downloads a file with progress reporting and verifies it against a MD5 checksum
        /// </summary>
        public async Task<FileDownloadResult> DownloadFileWithChecksumVerificationAsync(
            string url, 
            string destinationPath, 
            string expectedMd5Hash, 
            CancellationToken cancellationToken = default)
        {
            var result = await DownloadFileAsync(url, destinationPath, cancellationToken);
            
            if (result.Success && !string.IsNullOrEmpty(expectedMd5Hash))
            {
                // Calculate MD5 hash of downloaded file
                string actualHash = CalculateMd5(destinationPath);
                
                // Compare with expected hash (case-insensitive)
                if (!string.Equals(actualHash, expectedMd5Hash, StringComparison.OrdinalIgnoreCase))
                {
                    result.Success = false;
                    result.Error = new Exception($"MD5 checksum verification failed. Expected: {expectedMd5Hash}, Actual: {actualHash}");
                    
                    // Delete corrupted file
                    TryDeleteFile(destinationPath);
                    
                    // Raise error event for checksum failure
                    OnError(new FileDownloadErrorEventArgs(result.Error, destinationPath, url, result.TotalBytesDownloaded, result.StatusCode));
                    
                    // We don't raise the completion event here anymore since an error occurred
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Calculates MD5 hash for a file
        /// </summary>
        private string CalculateMd5(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
}