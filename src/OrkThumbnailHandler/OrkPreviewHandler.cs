using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpShell.Attributes;
using SharpShell.SharpPreviewHandler;

namespace OrkThumbnailHandler
{
    /// <summary>
    /// Shows .ork (OpenRocket Design Document) files in the Windows Explorer preview pane.
    ///
    /// The thumbnail handler shows the small, simplified thumbnail.png in file lists. The preview pane has room for
    /// the detailed preview.png, so this handler shows that instead, falling back to thumbnail.png and then to the
    /// OpenRocket icon.
    /// </summary>
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".ork")]
    [DisplayName("OpenRocket Design Document Preview Handler")]
    [Guid("7D3A002B-0D51-4DA1-86E6-3BBD9F0011DC")]
    // Use Windows' shared preview host. SharpShell's default, a dedicated host, registers an AppID that unregistering
    // does not remove.
    [PreviewHandler(SurrogateHostType = SurrogateHostType.Prevhost)]
    public class OrkPreviewHandler : SharpPreviewHandler
    {
        /// <summary>
        /// The size of the OpenRocket icon shown for files that contain no image.
        /// </summary>
        private const int FallbackIconSize = 256;

        protected override PreviewHandlerControl DoPreview()
        {
            return new ImagePreviewControl(LoadImage());
        }

        private Bitmap LoadImage()
        {
            try
            {
                using (var file = new FileStream(SelectedFilePath, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    var image = OrkImages.ReadImage(file, OrkImages.PreviewEntry, OrkImages.ThumbnailEntry);
                    if (image != null)
                    {
                        return image;
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                Log($"[OrkPreviewHandler] File is not a valid ZIP archive: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log($"[OrkPreviewHandler] Error extracting preview: {ex.Message}");
            }

            try
            {
                return OrkImages.GetFallbackIcon(FallbackIconSize);
            }
            catch (Exception ex)
            {
                Log($"[OrkPreviewHandler] Unable to use the fallback icon: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Draws an image centered in the preview pane, scaled down to fit but never scaled up, so that small images
        /// such as the fallback icon do not get blurry.
        /// </summary>
        private sealed class ImagePreviewControl : PreviewHandlerControl
        {
            private readonly Bitmap image;

            public ImagePreviewControl(Bitmap image)
            {
                this.image = image;
                DoubleBuffered = true;
                ResizeRedraw = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (image == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                {
                    return;
                }

                double scale = Math.Min(1.0, Math.Min((double)ClientSize.Width / image.Width,
                    (double)ClientSize.Height / image.Height));
                int width = Math.Max(1, (int)(image.Width * scale));
                int height = Math.Max(1, (int)(image.Height * scale));

                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.DrawImage(image, (ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2,
                    width, height);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    image?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
