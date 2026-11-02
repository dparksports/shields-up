namespace ShieldsUp.Core.Layout;

/// <summary>
/// Pure sizing maths for the main window, kept out of the shell so it can be
/// reasoned about (and checked) without a display.
///
/// Shields Up is meant to run right after a Windows install, which is exactly
/// when the display is least capable: the basic display adapter brings a
/// machine up at 800x600 until Windows Update lands the GPU driver, VMs and
/// remote sessions start at 1024x768, and TV-style panels often run 150-200%
/// scaling on a 720p/1080p mode. A fixed 1284x850 window with a 980x620 floor
/// puts the caption buttons and the right-hand actions off the visible desktop
/// in all of those states.
///
/// The rule here: never request a window larger than the work area we are
/// actually given, never set a floor above it, and treat "work area smaller
/// than the design" as a first-class layout (compact) rather than an error.
/// All values are device-independent pixels (DIPs); the caller converts.
/// </summary>
public static class WindowPlacement
{
    /// <summary>The size the layout was designed at (1284x850 DIPs).</summary>
    public const int DesignWidth = 1284;
    public const int DesignHeight = 850;

    /// <summary>Smallest width/height the responsive page layout is built for.</summary>
    public const int MinWidth = 720;
    public const int MinHeight = 520;

    /// <summary>Below this the shell switches to its compact presentation.</summary>
    public const int CompactWidth = 1000;
    public const int CompactHeight = 700;

    /// <summary>
    /// Resolves the window size to request at startup.
    /// </summary>
    /// <param name="workWidth">Work area width in DIPs (screen minus taskbar).</param>
    /// <param name="workHeight">Work area height in DIPs.</param>
    /// <param name="savedWidth">Last session's width in DIPs, if any.</param>
    /// <param name="savedHeight">Last session's height in DIPs, if any.</param>
    /// <returns>
    /// The size to request (never larger than the work area) and whether the
    /// window should open maximized because the work area is compact.
    /// </returns>
    public static Placement Compute(int workWidth, int workHeight, int? savedWidth, int? savedHeight)
    {
        // No usable display information: fall back to the design size rather
        // than collapsing to something silly.
        if (workWidth <= 0 || workHeight <= 0)
            return new Placement(DesignWidth, DesignHeight, false);

        // A floor above the work area is the bug this exists to prevent: on a
        // 800x600 desktop the floor becomes 800x560-ish, not 980x620.
        int minWidth = Math.Min(MinWidth, workWidth);
        int minHeight = Math.Min(MinHeight, workHeight);

        int width = Math.Clamp(savedWidth ?? DesignWidth, minWidth, workWidth);
        int height = Math.Clamp(savedHeight ?? DesignHeight, minHeight, workHeight);

        bool maximize = workWidth < CompactWidth || workHeight < CompactHeight;
        return new Placement(width, height, maximize);
    }

    /// <summary>A resolved startup placement, in DIPs.</summary>
    public readonly record struct Placement(int Width, int Height, bool Maximize);
}
