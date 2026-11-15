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
    /// .ork files are ZIP archives. OpenRocket stores a small, simplified rendering
    /// of the rocket for file icons as thumbnail.png at the root, next to the larger
    /// preview.png. This handler extracts thumbnail.png, or preview.png for files
    /// saved by older OpenRocket versions, and returns it as the file's thumbnail
    /// for display in Windows Explorer.
    /// </summary>
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".ork")]
    [DisplayName("OpenRocket Design Document Thumbnail Handler")]
    [Guid("D4E7F8A1-2B3C-4D5E-9F01-A2B3C4D5E6F7")]
    public class OrkThumbnailHandler : SharpThumbnailHandler
    {
        /// <summary>
        /// The paths of the images inside the .ork ZIP archive, in order of preference.
        /// </summary>
        private static readonly string[] ImageEntryNames = { "thumbnail.png", "preview.png" };

        /// <summary>
        /// The sizes of the OpenRocket icon embedded in this assembly, in ascending order. The icon is shown for files
        /// that contain no image. Each size is a copy of OpenRocket's icon-NNN.png, the same artwork as the
        /// icon-windows.ico that the installer uses as the .ork file type icon.
        /// </summary>
        private static readonly int[] FallbackIconSizes = { 16, 48, 64, 128, 256 };

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
                    var entry = FindImageEntry(archive);
                    if (entry != null)
                    {
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
            }
            catch (InvalidDataException ex)
            {
                LogError($"File is not a valid ZIP archive: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogError($"Error extracting thumbnail: {ex.Message}");
            }

            // No image in the file (or it could not be read): show the OpenRocket icon instead.
            try
            {
                return GetFallbackIcon((int)width);
            }
            catch (Exception ex)
            {
                LogError($"Unable to use the fallback icon: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Renders the embedded OpenRocket icon at the requested size, starting from the smallest embedded size
        /// that is at least that large so that it is only ever scaled down.
        /// </summary>
        private static Bitmap GetFallbackIcon(int maxSize)
        {
            int iconSize = FallbackIconSizes[FallbackIconSizes.Length - 1];
            foreach (var size in FallbackIconSizes)
            {
                if (size >= maxSize)
                {
                    iconSize = size;
                    break;
                }
            }

            string resourceName = $"OrkThumbnailHandler.Resources.icon-{iconSize:D3}.png";
            using (var stream = typeof(OrkThumbnailHandler).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Resource {resourceName} is missing.");
                }

                // Bitmap needs its stream for its whole lifetime, so ScaleImage's copy is what gets returned.
                return ScaleImage(new Bitmap(stream), maxSize);
            }
        }

        /// <summary>
        /// Finds the first image entry of <see cref="ImageEntryNames"/> that is present in the archive,
        /// using a case-insensitive search.
        /// </summary>
        private static ZipArchiveEntry FindImageEntry(ZipArchive archive)
        {
            foreach (var name in ImageEntryNames)
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.Equals(entry.FullName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Scales an image to fit within the requested dimensions, preserving aspect ratio.
        /// Takes ownership of <paramref name="original"/> and disposes it.
        /// </summary>
        private static Bitmap ScaleImage(Bitmap original, int maxSize)
        {
            if (original.Width <= maxSize && original.Height <= maxSize)
            {
                var copy = new Bitmap(original);
                original.Dispose();
                return copy;
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
