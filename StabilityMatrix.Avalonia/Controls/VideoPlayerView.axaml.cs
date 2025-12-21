using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Injectio.Attributes;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using StabilityMatrix.Avalonia.Models;

namespace StabilityMatrix.Avalonia.Controls;

[RegisterTransient<VideoPlayerView>]
public partial class VideoPlayerView : UserControl, IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static readonly StyledProperty<ImageSource?> SourceProperty = AvaloniaProperty.Register<
        VideoPlayerView,
        ImageSource?
    >(nameof(Source));

    public static readonly StyledProperty<bool> IsPlayingProperty = AvaloniaProperty.Register<
        VideoPlayerView,
        bool
    >(nameof(IsPlaying));

    public static readonly StyledProperty<double> PlaybackProgressProperty = AvaloniaProperty.Register<
        VideoPlayerView,
        double
    >(nameof(PlaybackProgress));

    public static readonly StyledProperty<bool> ShowControlsProperty = AvaloniaProperty.Register<
        VideoPlayerView,
        bool
    >(nameof(ShowControls), true);

    public static readonly StyledProperty<bool> AutoPlayProperty = AvaloniaProperty.Register<
        VideoPlayerView,
        bool
    >(nameof(AutoPlay), true);

    public static readonly StyledProperty<Thickness> VideoMarginProperty = AvaloniaProperty.Register<
        VideoPlayerView,
        Thickness
    >(nameof(VideoMargin), new Thickness(8));

    public static readonly StyledProperty<double> PlaybackRateProperty = AvaloniaProperty.Register<
        VideoPlayerView,
        double
    >(nameof(PlaybackRate), 1.0);

    private readonly LibVLC? libVlc;
    private MediaPlayer? mediaPlayer;
    private Media? activeMedia;
    private string? activeMediaPath;
    private VideoView? videoView;
    private bool isDisposed;
    private DispatcherTimer? progressTimer;

    public ImageSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double PlaybackProgress
    {
        get => GetValue(PlaybackProgressProperty);
        set => SetValue(PlaybackProgressProperty, value);
    }

    public bool ShowControls
    {
        get => GetValue(ShowControlsProperty);
        set => SetValue(ShowControlsProperty, value);
    }

    public bool AutoPlay
    {
        get => GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    public double PlaybackRate
    {
        get => GetValue(PlaybackRateProperty);
        set => SetValue(PlaybackRateProperty, value);
    }

    public Thickness VideoMargin
    {
        get => GetValue(VideoMarginProperty);
        set => SetValue(VideoMarginProperty, value);
    }

    static VideoPlayerView()
    {
        SourceProperty.Changed.AddClassHandler<VideoPlayerView>((view, args) => view.OnSourceChanged(args));
        PlaybackRateProperty.Changed.AddClassHandler<VideoPlayerView>(
            (view, args) => view.ApplyPlaybackRate()
        );
        AutoPlayProperty.Changed.AddClassHandler<VideoPlayerView>((view, args) => view.ApplyAutoPlay());
    }

    public VideoPlayerView()
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

            UpdateMedia(Source);
        }

        progressTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(200),
            DispatcherPriority.Background,
            ProgressTimerTick
        );
        progressTimer.Start();
    }

    private void OnPlayPauseButtonClick(object? sender, RoutedEventArgs e)
    {
        if (mediaPlayer is null)
        {
            return;
        }

        if (IsPlaying)
        {
            PauseVideo();
            return;
        }

        PlayVideo();
    }

    private void ProgressTimerTick(object? sender, EventArgs e)
    {
        UpdatePlaybackProgress();
        EnsureMediaLoops();
    }

    private void UpdatePlaybackProgress()
    {
        if (mediaPlayer is null || mediaPlayer.Length <= 0)
        {
            PlaybackProgress = 0;
            return;
        }

        var progress = mediaPlayer.Time / (double)mediaPlayer.Length * 100;
        PlaybackProgress = Math.Clamp(progress, 0, 100);
    }

    private void EnsureMediaLoops()
    {
        if (mediaPlayer is null)
        {
            return;
        }

        if (mediaPlayer.Length <= 0)
        {
            return;
        }

        // When playback naturally stops, IsPlaying becomes false and Time approaches Length.
        if (mediaPlayer.IsPlaying)
        {
            return;
        }

        if (mediaPlayer.Time < mediaPlayer.Length - 200)
        {
            return;
        }

        try
        {
            mediaPlayer.Time = 0;
            PlayVideo();
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to restart media playback");
        }
    }

    private void OnSourceChanged(AvaloniaPropertyChangedEventArgs args)
    {
        UpdateMedia((ImageSource?)args.NewValue);
    }

    private void UpdateMedia(ImageSource? source)
    {
        if (mediaPlayer is null)
        {
            return;
        }

        var filePath = source?.LocalFile?.FullPath;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || libVlc is null)
        {
            ClearMedia();
            return;
        }

        if (string.Equals(activeMediaPath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        mediaPlayer.Stop();

        activeMedia?.Dispose();
        activeMedia = new Media(libVlc, filePath, FromType.FromPath);
        activeMediaPath = filePath;

        mediaPlayer.Media = activeMedia;
        ApplyAutoPlay();
    }

    public void PlayVideo()
    {
        if (mediaPlayer is null)
        {
            return;
        }

        ApplyPlaybackRate();

        try
        {
            mediaPlayer.Play();
            IsPlaying = true;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to start media playback");
        }
    }

    public void PauseVideo()
    {
        if (mediaPlayer is null)
        {
            return;
        }

        try
        {
            mediaPlayer.Pause();
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to pause media playback");
        }
        finally
        {
            IsPlaying = false;
        }
    }

    private void ApplyAutoPlay()
    {
        if (mediaPlayer is null)
        {
            return;
        }

        if (AutoPlay)
        {
            PlayVideo();
            return;
        }

        PauseVideo();
    }

    private void ApplyPlaybackRate()
    {
        if (mediaPlayer is null)
        {
            return;
        }

        var safeRate = Math.Clamp(PlaybackRate, 0.1, 4.0);

        try
        {
            mediaPlayer.SetRate((float)safeRate);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to set playback rate");
        }
    }

    private void ClearMedia()
    {
        if (mediaPlayer is null)
        {
            return;
        }

        mediaPlayer.Stop();
        mediaPlayer.Media = null;

        activeMedia?.Dispose();
        activeMedia = null;
        activeMediaPath = null;

        IsPlaying = false;
        PlaybackProgress = 0;
    }

    private void OnMediaEndReached(object? sender, EventArgs args)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (mediaPlayer is null)
            {
                return;
            }

            mediaPlayer.Stop();
            Dispatcher.UIThread.Post(() => PlayVideo());
        });
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        ClearMedia();

        if (videoView is not null)
        {
            try
            {
                videoView.MediaPlayer = null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error while detaching VideoView media player");
            }

            videoView = null;
        }

        if (mediaPlayer is not null)
        {
            mediaPlayer.EndReached -= OnMediaEndReached;
            mediaPlayer.Dispose();
            mediaPlayer = null;
        }

        progressTimer?.Stop();
        progressTimer = null;

        GC.SuppressFinalize(this);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
