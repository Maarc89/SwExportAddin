using System;
using System.Drawing;
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

                using (Bitmap source = GetResourceBitmap(resourceFileName))
                {
                    if (source == null)
                    {
                        return null;
                    }

                    using (var target = new Bitmap(size, size))
                    using (var graphics = Graphics.FromImage(target))
                    {
                        graphics.Clear(Color.Transparent);
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

        private static Bitmap GetResourceBitmap(string resourceFileName)
        {
            switch (Path.GetFileNameWithoutExtension(resourceFileName))
            {
                case "ExportPlano":
                    return Properties.Resources.ExportPlano == null ? null : (Bitmap)Properties.Resources.ExportPlano.Clone();
                case "ExportCarpeta":
                    return Properties.Resources.ExportCarpeta == null ? null : (Bitmap)Properties.Resources.ExportCarpeta.Clone();
                case "ExportSelect":
                    return Properties.Resources.ExportSelect == null ? null : (Bitmap)Properties.Resources.ExportSelect.Clone();
                default:
                    return null;
            }
        }
    }
}
