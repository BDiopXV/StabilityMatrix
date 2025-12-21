using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Injectio.Attributes;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// A video thumbnail control that plays video on hover and shows a static preview when not hovering.
/// </summary>
[RegisterTransient<VideoThumbnailView>]
public partial class VideoThumbnailView : UserControl, IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static readonly StyledProperty<ImageSource?> SourceProperty = AvaloniaProperty.Register<
        VideoThumbnailView,
        ImageSource?
    >(nameof(Source));

    public static readonly StyledProperty<bool> AutoPlayProperty = AvaloniaProperty.Register<
        VideoThumbnailView,
        bool
    >(nameof(AutoPlay), true);

    public static readonly DirectProperty<VideoThumbnailView, Uri?> PreviewUriProperty =
        AvaloniaProperty.RegisterDirect<VideoThumbnailView, Uri?>(nameof(PreviewUri), o => o.previewUri);

    public static readonly DirectProperty<VideoThumbnailView, bool> IsHoveringProperty =
        AvaloniaProperty.RegisterDirect<VideoThumbnailView, bool>(nameof(IsHovering), o => o.isHovering);

    private Uri? previewUri;
    private bool isHovering;
    private readonly LibVLC? libVlc;
    private MediaPlayer? mediaPlayer;
    private Media? activeMedia;
    private VideoView? videoView;
    private bool isDisposed;

    public ImageSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool AutoPlay
    {
        get => GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    public Uri? PreviewUri
    {
        get => previewUri;
        private set => SetAndRaise(PreviewUriProperty, ref previewUri, value);
    }

    public bool IsHovering
    {
        get => isHovering;
        private set => SetAndRaise(IsHoveringProperty, ref isHovering, value);
    }

    static VideoThumbnailView()
    {
        SourceProperty.Changed.AddClassHandler<VideoThumbnailView>((view, _) => view.UpdatePreviewUri());
    }

    public VideoThumbnailView()
    {
        InitializeComponent();

        videoView = this.FindControl<VideoView>("PART_VideoView");

        if (!Design.IsDesignMode)
        {
            libVlc = App.Services?.GetService<LibVLC>();
        }

        if (libVlc is not null)
        {
            mediaPlayer = new MediaPlayer(libVlc) { EnableHardwareDecoding = true };
            mediaPlayer.EndReached += OnMediaEndReached;

            if (videoView is not null)
            {
                videoView.MediaPlayer = mediaPlayer;
            }
        }

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        IsHovering = true;
        StartPlayback();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        IsHovering = false;
        StopPlayback();
    }

    private void StartPlayback()
    {
        if (mediaPlayer is null || libVlc is null)
            return;

        var filePath = Source?.LocalFile?.FullPath;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        try
        {
            activeMedia?.Dispose();
            activeMedia = new Media(libVlc, new Uri(filePath));
            mediaPlayer.Play(activeMedia);
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Failed to start video playback for {Path}", filePath);
        }
    }

    private void StopPlayback()
    {
        if (mediaPlayer is null)
            return;

        try
        {
            mediaPlayer.Stop();
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Failed to stop video playback");
        }
    }

    private void OnMediaEndReached(object? sender, EventArgs e)
    {
        // Loop the video by restarting from the beginning
        Dispatcher.UIThread.Post(() =>
        {
            if (IsHovering && mediaPlayer is not null && activeMedia is not null)
            {
                mediaPlayer.Stop();
                mediaPlayer.Play(activeMedia);
            }
        });
    }

    private void UpdatePreviewUri()
    {
        var filePath = Source?.LocalFile?.FullPath;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            PreviewUri = null;
            return;
        }

        // Look for sidecar PNG preview (same name, .png extension)
        var sidecarPath = Path.ChangeExtension(filePath, ".png");
        if (File.Exists(sidecarPath))
        {
            PreviewUri = new Uri(sidecarPath);
            return;
        }

        // Try to generate the sidecar preview if it doesn't exist
        try
        {
            ImageMetadata.TryWriteVideoPreviewSidecar(
                new StabilityMatrix.Core.Models.FileInterfaces.FilePath(filePath)
            );
            if (File.Exists(sidecarPath))
            {
                PreviewUri = new Uri(sidecarPath);
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Failed to generate video preview for {Path}", filePath);
        }

        PreviewUri = null;
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;

        PointerEntered -= OnPointerEntered;
        PointerExited -= OnPointerExited;

        if (mediaPlayer is not null)
        {
            mediaPlayer.EndReached -= OnMediaEndReached;
            mediaPlayer.Stop();
            mediaPlayer.Dispose();
            mediaPlayer = null;
        }

        activeMedia?.Dispose();
        activeMedia = null;

        GC.SuppressFinalize(this);
    }
}
