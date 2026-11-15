using System.Drawing;

namespace TouchSimulator;

/// <summary>
/// Maps a point inside a zoomed/letterboxed screenshot viewer back to the
/// captured image pixel. Does not depend on the PictureBox or the driver.
/// </summary>
public static class PreviewCoordinateMapper
{
    public static bool TryMap(
        Point mouse, Size viewer, Size image, Rectangle capturedScreenBounds,
        bool targetRelative, out Point commandPoint, out Point screenPoint)
    {
        commandPoint = Point.Empty;
        screenPoint = Point.Empty;
        if (viewer.Width <= 0 || viewer.Height <= 0 || image.Width <= 0 || image.Height <= 0 ||
            capturedScreenBounds.Width != image.Width || capturedScreenBounds.Height != image.Height)
            return false;

        double scale = Math.Min((double)viewer.Width / image.Width, (double)viewer.Height / image.Height);
        double renderedWidth = image.Width * scale, renderedHeight = image.Height * scale;
        double left = (viewer.Width - renderedWidth) / 2.0;
        double top = (viewer.Height - renderedHeight) / 2.0;
        if (mouse.X < left || mouse.Y < top || mouse.X >= left + renderedWidth || mouse.Y >= top + renderedHeight)
            return false;
        int x = Math.Clamp((int)((mouse.X - left) / scale), 0, image.Width - 1);
        int y = Math.Clamp((int)((mouse.Y - top) / scale), 0, image.Height - 1);
        screenPoint = new Point(capturedScreenBounds.Left + x, capturedScreenBounds.Top + y);
        commandPoint = targetRelative ? new Point(x, y) : screenPoint;
        return true;
    }
}
