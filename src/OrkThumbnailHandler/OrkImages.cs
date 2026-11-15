using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;

namespace OrkThumbnailHandler
{
    /// <summary>
    /// Reads the images that OpenRocket stores in .ork files, and the OpenRocket icon shown for files without one.
    ///
    /// .ork files are ZIP archives. OpenRocket stores a small, simplified rendering of the rocket for file icons as
    /// thumbnail.png at the root, next to the larger, detailed preview.png. Files saved by older OpenRocket versions
    /// have only preview.png, or no image at all.
    /// </summary>
    internal static class OrkImages
    {
        public const string ThumbnailEntry = "thumbnail.png";
        public const string PreviewEntry = "preview.png";

        /// <summary>
        /// The sizes of the OpenRocket icon embedded in this assembly, in ascending order. Each size is a copy of
        /// OpenRocket's icon-NNN.png, the same artwork as the icon-windows.ico that the installer uses as the .ork file
        /// type icon.
        /// </summary>
        private static readonly int[] FallbackIconSizes = { 16, 48, 64, 128, 256 };

        /// <summary>
        /// Reads the first of <paramref name="entryNames"/> that is present at the root of the .ork archive, using a
        /// case-insensitive search.
        /// </summary>
        /// <returns>The image, or null if the archive contains none of the entries.</returns>
        /// <exception cref="InvalidDataException">The file is not a valid ZIP archive.</exception>
        public static Bitmap ReadImage(Stream orkFile, params string[] entryNames)
        {
            using (var archive = new ZipArchive(orkFile, ZipArchiveMode.Read, leaveOpen: true))
            {
                var entry = FindEntry(archive, entryNames);
                if (entry == null)
                {
                    return null;
                }

                using (var entryStream = entry.Open())
                using (var memoryStream = new MemoryStream())
                {
                    // Copy to a MemoryStream first: ZipArchive streams don't support seeking, which Bitmap needs.
                    entryStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    // Bitmap needs its stream for its whole lifetime, so return a copy that doesn't.
                    using (var image = new Bitmap(memoryStream))
                    {
                        return new Bitmap(image);
                    }
                }
            }
        }

        /// <summary>
        /// Renders the embedded OpenRocket icon at the requested size, starting from the smallest embedded size
        /// that is at least that large so that it is only ever scaled down.
        /// </summary>
        public static Bitmap GetFallbackIcon(int maxSize)
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
            using (var stream = typeof(OrkImages).Assembly.GetManifestResourceStream(resourceName))
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
        /// Scales an image to fit within the requested dimensions, preserving aspect ratio.
        /// Takes ownership of <paramref name="original"/> and disposes it.
        /// </summary>
        public static Bitmap ScaleImage(Bitmap original, int maxSize)
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

        private static ZipArchiveEntry FindEntry(ZipArchive archive, string[] entryNames)
        {
            foreach (var name in entryNames)
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
    }
}
