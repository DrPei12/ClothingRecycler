namespace ClothingRecycler.Desktop.Controls
{
    public sealed partial class ImportPreviewDialog : ContentDialog
    {
        public ImportPreviewDialog(ImportPreviewModel preview)
        {
            Preview = preview;
            InitializeComponent();

            Title = $"{preview.EntityDisplayName}导入预览";
            PrimaryButtonText = preview.CanImport ? "确认导入" : string.Empty;
            CloseButtonText = preview.CanImport ? "取消" : "关闭";
            DefaultButton = preview.CanImport ? ContentDialogButton.Primary : ContentDialogButton.Close;
        }

        public ImportPreviewModel Preview { get; }

        public string SummaryActionText => $"{Preview.InsertCount} / {Preview.UpdateCount}";
    }
}
