using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ImageGalleryCard))]
[ManagedService]
[RegisterTransient<ImageGalleryCardViewModel>]
public partial class ImageGalleryCardViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty]
    private bool isPreviewOverlayEnabled;

    [ObservableProperty]
    private Bitmap? previewImage;

    [ObservableProperty]
    private AvaloniaList<ImageSource> imageSources = new();

    [ObservableProperty]
    private ImageSource? selectedImage;

    [ObservableProperty]
    private bool isVideoPlayerEnabled;

    [ObservableProperty]
    private bool isVideoPlayerReady = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigateBack), nameof(CanNavigateForward))]
    private int selectedImageIndex;

    [ObservableProperty]
    private bool isPixelGridEnabled;

    public bool HasMultipleImages => ImageSources.Count > 1;

    public bool CanNavigateBack => SelectedImageIndex > 0;
    public bool CanNavigateForward => SelectedImageIndex < ImageSources.Count - 1;

    public bool IsVideoPlayerVisible =>
        IsVideoPlayerEnabled
        && IsVideoPlayerReady
        && SelectedImage?.TemplateKey == ImageSourceTemplateType.Video;

    public bool IsImageCarouselVisible => !IsVideoPlayerVisible;

    public ImageGalleryCardViewModel(
        IServiceManager<ViewModelBase> vmFactory,
        ISettingsManager settingsManager
    )
    {
        this.vmFactory = vmFactory;

        IsPixelGridEnabled = settingsManager.Settings.IsImageViewerPixelGridEnabled;

        settingsManager.RegisterPropertyChangedHandler(
            s => s.IsImageViewerPixelGridEnabled,
            newValue =>
            {
                IsPixelGridEnabled = newValue;
            }
        );

        ImageSources.CollectionChanged += OnImageSourcesItemsChanged;
    }

    public void SetPreviewImage(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            Logger.Warn("SetPreviewImage: imageBytes is null or empty");
            return;
        }

        try
        {
            using var stream = new MemoryStream(imageBytes);
            stream.Seek(0, SeekOrigin.Begin); // Ensure stream is at the beginning

            var bitmap = new Bitmap(stream);
            ApplyPreviewBitmap(bitmap);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to set preview image");
        }
    }

    private void OnImageSourcesItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is AvaloniaList<ImageSource> sources)
        {
            if (
                e.Action
                is NotifyCollectionChangedAction.Add
                    or NotifyCollectionChangedAction.Remove
                    or NotifyCollectionChangedAction.Reset
            )
            {
                if (sources.Count == 0)
                {
                    SelectedImageIndex = 0;
                }
                else if (SelectedImageIndex == -1)
                {
                    SelectedImageIndex = 0;
                }
                // Clamp the selected index to the new range
                else
                {
                    SelectedImageIndex = Math.Clamp(SelectedImageIndex, 0, sources.Count - 1);
                }
                OnPropertyChanged(nameof(CanNavigateBack));
                OnPropertyChanged(nameof(CanNavigateForward));
                OnPropertyChanged(nameof(HasMultipleImages));
            }

            if (e.NewItems is not null)
            {
                foreach (var newSource in e.NewItems.OfType<ImageSource>())
                {
                    newSource.RefreshVideoPreview();
                    RefreshTemplateKeyAsync(newSource);
                }
            }
        }
    }

    partial void OnSelectedImageChanged(ImageSource? value)
    {
        UpdatePreviewForSelectedImage(value);
        OnPropertyChanged(nameof(IsVideoPlayerVisible));
        OnPropertyChanged(nameof(IsImageCarouselVisible));
    }

    partial void OnIsVideoPlayerEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsVideoPlayerVisible));
        OnPropertyChanged(nameof(IsImageCarouselVisible));
    }

    partial void OnIsVideoPlayerReadyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsVideoPlayerVisible));
        OnPropertyChanged(nameof(IsImageCarouselVisible));
    }

    private void RefreshTemplateKeyAsync(ImageSource imageSource)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await imageSource.GetOrRefreshTemplateKeyAsync();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to refresh template key for {Uri}", imageSource.Uri);
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(imageSource, SelectedImage))
                {
                    OnPropertyChanged(nameof(IsVideoPlayerVisible));
                    OnPropertyChanged(nameof(IsImageCarouselVisible));
                }
            });
        });
    }

    private void UpdatePreviewForSelectedImage(ImageSource? value)
    {
        if (value?.VideoPreviewUri?.IsFile ?? false)
        {
            try
            {
                var bitmap = new Bitmap(value.VideoPreviewUri.LocalPath);
                ApplyPreviewBitmap(bitmap);
                return;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load saved preview for {Uri}", value.Uri);
            }
        }

        ApplyPreviewBitmap(null);
    }

    private void ApplyPreviewBitmap(Bitmap? newBitmap)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyPreviewBitmapCore(newBitmap);
            return;
        }

        Dispatcher.UIThread.Post(() => ApplyPreviewBitmapCore(newBitmap));
    }

    private void ApplyPreviewBitmapCore(Bitmap? newBitmap)
    {
        var currentImage = PreviewImage;
        PreviewImage = newBitmap;
        IsPreviewOverlayEnabled = newBitmap is not null;
        currentImage?.Dispose();
    }

    [RelayCommand]
    // ReSharper disable once UnusedMember.Local
    private async Task FlyoutCopy(IImage? image)
    {
        if (image is null)
        {
            Logger.Trace("FlyoutCopy: image is null");
            return;
        }

        Logger.Trace($"FlyoutCopy is copying bitmap...");

        await Task.Run(() =>
        {
            if (Compat.IsWindows)
            {
                WindowsClipboard.SetBitmap((Bitmap)image);
            }
        });
    }

    [RelayCommand]
    // ReSharper disable once UnusedMember.Local
    private async Task FlyoutPreview(IImage? image)
    {
        if (image is null)
        {
            Logger.Trace("FlyoutPreview: image is null");
            return;
        }

        Logger.Trace($"FlyoutPreview opening...");

        var viewerVm = vmFactory.Get<ImageViewerViewModel>();
        viewerVm.ImageSource = new ImageSource((Bitmap)image);

        var dialog = new BetterContentDialog { Content = new ImageViewerDialog { DataContext = viewerVm } };

        await dialog.ShowAsync();
    }
}
