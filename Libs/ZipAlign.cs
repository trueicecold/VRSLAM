using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;

namespace VRSLAM.Libs
{
    public class ZipAlign
    { 
        private const int ALIGNMENT = 4; // Standard alignment is 4 bytes
        public static void AlignZipFile(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Input file not found", inputPath);
                
            if (File.Exists(outputPath))
                File.Delete(outputPath);
                
            using (FileStream inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
            using (FileStream outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                AlignZipStream(inputStream, outputStream);
            }
            
            Console.WriteLine($"Aligned zip file created at: {outputPath}");
        }
        
        private static void AlignZipStream(Stream inputStream, Stream outputStream)
        {
            // First, read the entire zip file structure
            var entries = ReadZipEntries(inputStream);
            
            // Now write the aligned version
            WriteAlignedZip(entries, inputStream, outputStream);
        }
        
        private static List<ZipEntryInfo> ReadZipEntries(Stream zipStream)
        {
            var entries = new List<ZipEntryInfo>();
            
            using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
            {
                foreach (var entry in zipArchive.Entries)
                {
                    // Determine if the entry is compressed by looking at the compression method
                    // In ZIP format, method 0 means "stored" (no compression)
                    // Method 8 typically means "deflated" (compressed)
                    bool isCompressed = DetermineIfCompressed(entry);
                    
                    var entryInfo = new ZipEntryInfo
                    {
                        Name = entry.FullName,
                        CompressedSize = entry.CompressedLength,
                        UncompressedSize = entry.Length,
                        CompressionMethod = isCompressed
                    };
                    
                    entries.Add(entryInfo);
                }
            }
            
            // Reset stream position for further processing
            zipStream.Position = 0;
            return entries;
        }
        
        private static bool DetermineIfCompressed(ZipArchiveEntry entry)
        {
            // ZipArchiveEntry doesn't expose the compression method directly
            // So we'll use a heuristic: if compressed size < uncompressed size, it's likely compressed
            // Another option is to read the entry header directly, but that's more complex
            return entry.CompressedLength < entry.Length;
        }
        
        private static void WriteAlignedZip(List<ZipEntryInfo> entries, Stream inputStream, Stream outputStream)
        {
            using (var tempStream = new MemoryStream())
            {
                // Create a new zip file with aligned entries
                using (var zipArchive = new ZipArchive(tempStream, ZipArchiveMode.Create, true))
                {
                    foreach (var entryInfo in entries)
                    {
                        // Choose compression level based on original entry
                        CompressionLevel compressionLevel = entryInfo.CompressionMethod ? 
                            CompressionLevel.Optimal : CompressionLevel.NoCompression;
                            
                        // For uncompressed files, we need to ensure alignment
                        if (!entryInfo.CompressionMethod)
                        {
                            // Calculate padding needed for alignment
                            long currentPosition = tempStream.Length;
                            int padding = CalculatePadding(currentPosition, ALIGNMENT);
                            
                            // Add padding if needed
                            if (padding > 0)
                            {
                                byte[] paddingBytes = new byte[padding];
                                tempStream.Write(paddingBytes, 0, padding);
                                tempStream.Flush();
                            }
                        }
                        
                        // Create new entry with appropriate compression
                        var newEntry = zipArchive.CreateEntry(entryInfo.Name, compressionLevel);
                        
                        // Copy content from original file
                        using (var sourceArchive = new ZipArchive(inputStream, ZipArchiveMode.Read, true))
                        {
                            var sourceEntry = sourceArchive.GetEntry(entryInfo.Name);
                            if (sourceEntry != null)
                            {
                                using (var sourceStream = sourceEntry.Open())
                                using (var targetStream = newEntry.Open())
                                {
                                    sourceStream.CopyTo(targetStream);
                                }
                            }
                        }
                        
                        // Reset input stream for next entry
                        inputStream.Position = 0;
                    }
                }
                
                // Write the aligned zip to the output stream
                tempStream.Position = 0;
                tempStream.CopyTo(outputStream);
            }
        }
        
        private static int CalculatePadding(long position, int alignment)
        {
            int remainder = (int)(position % alignment);
            return remainder == 0 ? 0 : alignment - remainder;
        }
    }
            
    public class ZipEntryInfo
    {
        public string Name { get; set; }
        public long CompressedSize { get; set; }
        public long UncompressedSize { get; set; }
        public bool CompressionMethod { get; set; } // true for compressed, false for stored
    }
}