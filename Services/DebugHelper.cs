using System;
using System.Diagnostics;
using System.Windows;

namespace WpfPlannerApp.Services
{
    public static class DebugHelper
    {
        public static void Log(string message)
        {
            Debug.WriteLine($"[DEBUG {DateTime.Now:HH:mm:ss}] {message}");
        }

        public static void Show(string message)
        {
            MessageBox.Show(message, "Отладка", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void LogError(Exception ex)
        {
            Debug.WriteLine($"[ERROR] {ex.Message}");
            Show("Произошла ошибка: " + ex.Message);
        }
    }
}
