using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using SharpShell.Attributes;
using SharpShell.SharpThumbnailHandler;

namespace OrkThumbnailHandler
{
    /// <summary>
    /// Provides thumbnail images for .ork (OpenRocket Design Document) files.
    /// 
    /// .ork files are ZIP archives containing a preview.png image at the root.
    /// This handler extracts that image and returns it as the file's thumbnail
    /// for display in Windows Explorer.
    /// </summary>
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.FileExtension, ".ork")]
    [DisplayName("OpenRocket Design Document Thumbnail Handler")]
    [Guid("D4E7F8A1-2B3C-4D5E-9F01-A2B3C4D5E6F7")]
    public class OrkThumbnailHandler : SharpThumbnailHandler
    {
        /// <summary>
        /// The path of the preview image inside the .ork ZIP archive.
        /// </summary>
        private const string PreviewEntryName = "preview.png";

        /// <summary>
        /// Gets the thumbnail image for the given .ork file.
        /// </summary>
        /// <param name="width">The maximum width/height of the thumbnail requested by the shell.</param>
        /// <returns>A Bitmap containing the thumbnail, or null if no preview is available.</returns>
        protected override Bitmap GetThumbnailImage(uint width)
        {
            try
            {
                // SelectedItemStream is provided by SharpShell — it's a stream to the file
                // that Explorer is asking us to thumbnail.
                using (var archive = new ZipArchive(SelectedItemStream, ZipArchiveMode.Read))
                {
                    var entry = FindPreviewEntry(archive);
                    if (entry == null)
                    {
                        LogError("No preview.png found in .ork archive.");
                        return null;
                    }

                    using (var entryStream = entry.Open())
                    using (var memoryStream = new MemoryStream())
                    {
                        // Copy to a MemoryStream first — ZipArchive streams don't
                        // support seeking, which Bitmap's constructor may need.
                        entryStream.CopyTo(memoryStream);
                        memoryStream.Position = 0;

                        var original = new Bitmap(memoryStream);
                        return ScaleImage(original, (int)width);
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                LogError($"File is not a valid ZIP archive: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogError($"Error extracting thumbnail: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds the preview image entry in the archive, using a case-insensitive search.
        /// </summary>
        private static ZipArchiveEntry FindPreviewEntry(ZipArchive archive)
        {
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, PreviewEntryName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        /// <summary>
        /// Scales an image to fit within the requested dimensions, preserving aspect ratio.
        /// </summary>
        private static Bitmap ScaleImage(Bitmap original, int maxSize)
        {
            if (original.Width <= maxSize && original.Height <= maxSize)
            {
                return new Bitmap(original);
            }

            double ratioX = (double)maxSize / original.Width;
            double ratioY = (double)maxSize / original.Height;
            double ratio = Math.Min(ratioX, ratioY);

            int newWidth = (int)(original.Width * ratio);
            int newHeight = (int)(original.Height * ratio);

            var scaled = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(original, 0, 0, newWidth, newHeight);
            }

            original.Dispose();
            return scaled;
        }

        /// <summary>
        /// Logs an error via SharpShell's logging infrastructure.
        /// </summary>
        private void LogError(string message)
        {
            // SharpShell logs to the Windows Event Log when available.
            // In debug builds, this also writes to the debug output.
            Log($"[OrkThumbnailHandler] {message}");
        }
    }
}
