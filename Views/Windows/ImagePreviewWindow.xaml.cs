using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Storyboard.Views.Windows;

public partial class ImagePreviewWindow : Window
{
    public ImagePreviewWindow(string imagePath)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                PreviewImage.Source = bitmap;
                Title = $"预览 - {Path.GetFileName(imagePath)}";
            }
            catch
            {
                // ignore; window will show empty
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
