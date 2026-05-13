using System;
using System.IO;
using System.Windows.Forms;

namespace SwExportAddin
{
    internal static class ExportProcessHelper
    {
        public static FileProcessResult ProcessFileWithConflictPrompt(ExportProgressDialog progress, ExportFileProcessor fileProcessor, string file, bool exportPdf, bool exportDwg)
        {
            if (progress != null && progress.CancelRequested)
            {
                return FileProcessResult.Cancelled;
            }

            string folder = Path.GetDirectoryName(file);
            string baseName = Path.GetFileNameWithoutExtension(file);

            if (fileProcessor.NeedConflictPrompt(file, exportPdf, exportDwg))
            {
                using (var dlg = new ExistingFileConflictDialog(Path.GetFileName(file), folder))
                {
                    var result = dlg.ShowDialog(progress);
                    if (result != DialogResult.OK)
                    {
                        return FileProcessResult.Cancelled;
                    }

                    if (dlg.Decision == ExistingFileDecision.Skip)
                    {
                        return FileProcessResult.Skipped;
                    }

                    if (dlg.Decision == ExistingFileDecision.Cancel)
                    {
                        return FileProcessResult.Cancelled;
                    }

                    if (dlg.Decision == ExistingFileDecision.Overwrite)
                    {
                        TryDelete(Path.Combine(folder, baseName + ".pdf"));
                        TryDelete(Path.Combine(folder, baseName + ".dwg"));
                    }

                    if (dlg.Decision == ExistingFileDecision.Rename)
                    {
                        int suffix = 1;
                        while (File.Exists(Path.Combine(folder, baseName + "_" + suffix + ".pdf")) || File.Exists(Path.Combine(folder, baseName + "_" + suffix + ".dwg")))
                        {
                            suffix++;
                        }

                        baseName = baseName + "_" + suffix;
                    }
                }
            }

            bool pdfOk = true;
            bool dwgOk = true;
            string failureReason;

            if (exportPdf)
            {
                pdfOk = fileProcessor.ExportPdf(file, Path.Combine(folder, baseName + ".pdf"), out failureReason);
                if (!pdfOk)
                {
                    return FileProcessResult.Failed;
                }
            }

            if (exportDwg)
            {
                dwgOk = fileProcessor.ExportDwg(file, Path.Combine(folder, baseName + ".dwg"), out failureReason);
                if (!dwgOk)
                {
                    return FileProcessResult.Failed;
                }
            }

            return (pdfOk && dwgOk) ? FileProcessResult.Success : FileProcessResult.Failed;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
