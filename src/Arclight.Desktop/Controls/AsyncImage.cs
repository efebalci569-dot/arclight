using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Arclight.Desktop.Controls;

/// <summary>
/// Loads an image file off the UI thread, decoded at the size it is shown and cached in memory.
/// A missing or unreadable file simply leaves the image empty so the placeholder underneath shows.
/// </summary>
public static class AsyncImage
{
    private const int CacheCapacity = 600;
    private static readonly Dictionary<string, LinkedListNode<(string Key, BitmapSource Image)>> Cache = [];
    private static readonly LinkedList<(string Key, BitmapSource Image)> Recent = new();
    private static readonly SemaphoreSlim DecodeSlots = new(Math.Max(2, Environment.ProcessorCount / 2));
    private static readonly object CacheLock = new();

    /// <summary>Set by the theme service when the user turns on "Reduce animations".</summary>
    public static bool ReduceMotion { get; set; }

    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
        "Path", typeof(string), typeof(AsyncImage), new PropertyMetadata(null, OnPathChanged));

    public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.RegisterAttached(
        "DecodeWidth", typeof(int), typeof(AsyncImage), new PropertyMetadata(0));

    public static readonly DependencyProperty HasImageProperty = DependencyProperty.RegisterAttached(
        "HasImage", typeof(bool), typeof(AsyncImage), new PropertyMetadata(false));

    public static string? GetPath(DependencyObject element) => (string?)element.GetValue(PathProperty);

    public static void SetPath(DependencyObject element, string? value) => element.SetValue(PathProperty, value);

    public static int GetDecodeWidth(DependencyObject element) => (int)element.GetValue(DecodeWidthProperty);

    public static void SetDecodeWidth(DependencyObject element, int value) => element.SetValue(DecodeWidthProperty, value);

    public static bool GetHasImage(DependencyObject element) => (bool)element.GetValue(HasImageProperty);

    public static void SetHasImage(DependencyObject element, bool value) => element.SetValue(HasImageProperty, value);

    /// <summary>Drops cached bitmaps (for example after the artwork cache was cleared).</summary>
    public static void ClearMemoryCache()
    {
        lock (CacheLock)
        {
            Cache.Clear();
            Recent.Clear();
        }
    }

    private static async void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image)
        {
            return;
        }

        var path = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(path))
        {
            image.Source = null;
            SetHasImage(image, false);
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(image).DpiScaleX;
        var decodeWidth = (int)Math.Round(GetDecodeWidth(image) * Math.Max(1, dpi));
        var key = $"{path}|{decodeWidth}";

        if (TryGetCached(key) is { } cached)
        {
            image.BeginAnimation(UIElement.OpacityProperty, null);
            image.Source = cached;
            image.Opacity = 1;
            SetHasImage(image, true);
            return;
        }

        image.Source = null;
        SetHasImage(image, false);
        var bitmap = await Task.Run(() => Decode(path, decodeWidth));

        // The element may have been recycled for another game while we were decoding.
        if (!string.Equals(GetPath(image), path, StringComparison.Ordinal) || bitmap is null)
        {
            return;
        }

        AddToCache(key, bitmap);
        image.Source = bitmap;
        SetHasImage(image, true);
        if (ReduceMotion)
        {
            image.Opacity = 1;
        }
        else
        {
            image.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        }
    }

    private static async Task<BitmapSource?> Decode(string path, int decodeWidth)
    {
        await DecodeSlots.WaitAsync();
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            // Read into memory so the file is never kept locked.
            var bytes = await File.ReadAllBytesAsync(path);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.StreamSource = stream;
            if (decodeWidth > 0)
            {
                bitmap.DecodePixelWidth = decodeWidth;
            }

            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or ArgumentException or FileFormatException)
        {
            return null;
        }
        finally
        {
            DecodeSlots.Release();
        }
    }

    private static BitmapSource? TryGetCached(string key)
    {
        lock (CacheLock)
        {
            if (!Cache.TryGetValue(key, out var node))
            {
                return null;
            }

            Recent.Remove(node);
            Recent.AddFirst(node);
            return node.Value.Image;
        }
    }

    private static void AddToCache(string key, BitmapSource image)
    {
        lock (CacheLock)
        {
            if (Cache.ContainsKey(key))
            {
                return;
            }

            Cache[key] = Recent.AddFirst((key, image));
            while (Recent.Count > CacheCapacity)
            {
                var last = Recent.Last!;
                Recent.RemoveLast();
                Cache.Remove(last.Value.Key);
            }
        }
    }
}
