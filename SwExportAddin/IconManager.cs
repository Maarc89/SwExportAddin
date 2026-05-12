using System;
using System.IO;

namespace SwExportAddin
{
    internal static class IconManager
    {
        private static readonly string IconDirectory = Path.Combine(Path.GetTempPath(), "SwExportAddin", "Icons");

        public static string GetIconFile(string resourceFileName, int size)
        {
            try
            {
                Directory.CreateDirectory(IconDirectory);

                string outputFile = Path.Combine(IconDirectory, Path.GetFileNameWithoutExtension(resourceFileName) + "_" + size + ".bmp");
                if (File.Exists(outputFile))
                {
                    return outputFile;
                }

                var assembly = typeof(IconManager).Assembly;
                string resourceName = null;
                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = name;
                        break;
                    }
                }

                if (resourceName == null)
                {
                    return null;
                }

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (var source = new System.Drawing.Bitmap(stream))
                    using (var target = new System.Drawing.Bitmap(size, size))
                    using (var graphics = System.Drawing.Graphics.FromImage(target))
                    {
                        graphics.Clear(System.Drawing.Color.Transparent);
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphics.DrawImage(source, 0, 0, size, size);
                        target.Save(outputFile, System.Drawing.Imaging.ImageFormat.Bmp);
                    }
                }

                return outputFile;
            }
            catch
            {
                return null;
            }
        }
    }
}
