using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using SharpShell.Attributes;
using SharpShell.SharpThumbnailHandler;

namespace OrkThumbnailHandler
{
    /// <summary>
    /// Provides thumbnail images for .ork (OpenRocket Design Document) files.
    ///
    /// This handler returns the file's thumbnail.png, or preview.png for files saved by older OpenRocket versions,
    /// as the file's thumbnail in Windows Explorer. Files without either image get the OpenRocket icon.
    /// </summary>
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".ork")]
    [DisplayName("OpenRocket Design Document Thumbnail Handler")]
    [Guid("D4E7F8A1-2B3C-4D5E-9F01-A2B3C4D5E6F7")]
    public class OrkThumbnailHandler : SharpThumbnailHandler
    {
        /// <summary>
        /// Gets the thumbnail image for the given .ork file.
        /// </summary>
        /// <param name="width">The maximum width/height of the thumbnail requested by the shell.</param>
        /// <returns>A Bitmap containing the thumbnail, or null if no image is available.</returns>
        protected override Bitmap GetThumbnailImage(uint width)
        {
            try
            {
                // SelectedItemStream is provided by SharpShell: a stream to the file Explorer wants a thumbnail for.
                var image = OrkImages.ReadImage(SelectedItemStream, OrkImages.ThumbnailEntry, OrkImages.PreviewEntry);
                if (image != null)
                {
                    return OrkImages.ScaleImage(image, (int)width);
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
                return OrkImages.GetFallbackIcon((int)width);
            }
            catch (Exception ex)
            {
                LogError($"Unable to use the fallback icon: {ex.Message}");
                return null;
            }
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
