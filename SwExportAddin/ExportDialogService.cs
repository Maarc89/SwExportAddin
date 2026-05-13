using System.Windows.Forms;

namespace SwExportAddin
{
    internal sealed class ExportDialogService
    {
        public bool AskExportFormats(string title, out bool exportPdf, out bool exportDwg)
        {
            using (var dlg = new ExportOptionsDialog(title))
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    exportPdf = false;
                    exportDwg = false;
                    return false;
                }

                exportPdf = dlg.ExportPdf;
                exportDwg = dlg.ExportDwg;
                return true;
            }
        }
    }
}
