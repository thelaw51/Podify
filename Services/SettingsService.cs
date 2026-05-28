namespace Podify.Services;

/// <summary>User-facing playback preferences backed by platform key-value storage.</summary>
public class SettingsService
{
    private const string KeySkipForward = "skip_forward_seconds";
    private const string KeySkipBack = "skip_back_seconds";
    private const string KeyDefaultSpeed = "default_speed";

    public TimeSpan SkipForwardDuration
    {
        get => TimeSpan.FromSeconds(Preferences.Default.Get(KeySkipForward, 30.0));
        set => Preferences.Default.Set(KeySkipForward, value.TotalSeconds);
    }

    public TimeSpan SkipBackDuration
    {
        get => TimeSpan.FromSeconds(Preferences.Default.Get(KeySkipBack, 15.0));
        set => Preferences.Default.Set(KeySkipBack, value.TotalSeconds);
    }

    public double DefaultSpeed
    {
        get => Preferences.Default.Get(KeyDefaultSpeed, 1.0);
        set => Preferences.Default.Set(KeyDefaultSpeed, Math.Clamp(value, 0.5, 3.0));
    }
}
