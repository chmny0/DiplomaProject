using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace WpfPlannerApp.Services
{
    public static class FileService
    {
        private static readonly string _documentsFolder;

        static FileService()
        {
            _documentsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Documents");
            if (!Directory.Exists(_documentsFolder))
                Directory.CreateDirectory(_documentsFolder);
        }

        public static string? PickAndSaveFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите документ",
                Filter = "Все документы|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.jpg;*.jpeg;*.png;*.bmp|PDF|*.pdf|Word|*.doc;*.docx|Изображения|*.jpg;*.jpeg;*.png;*.bmp",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() != true)
                return null;

            string sourcePath = dialog.FileName;
            string extension = Path.GetExtension(sourcePath);
            string originalName = Path.GetFileNameWithoutExtension(sourcePath);
            string safeName = SanitizeFileName(originalName);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string uniqueName = $"{safeName}_{timestamp}{extension}";
            string destPath = Path.Combine(_documentsFolder, uniqueName);

            File.Copy(sourcePath, destPath, overwrite: true);

            return destPath;
        }

        public static void OpenFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                MessageBox.Show("Файл не найден", "Ошибка");
                return;
            }

            if (!File.Exists(filePath))
            {
                MessageBox.Show($"Файл не найден:\n{filePath}", "Ошибка");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть файл: " + ex.Message, "Ошибка");
            }
        }

        public static string GetFileName(string? filePath)
        {
            return string.IsNullOrWhiteSpace(filePath) ? "" : Path.GetFileName(filePath);
        }

        public static string GetDocumentsFolder() => _documentsFolder;

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized.Trim();
        }
        public static string GetStorageFolder()
        {
            string folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Documents");
            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);
            return folder;
        }
    }
}