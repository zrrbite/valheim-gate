using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The hover tooltip on the HUD's boon and way rows. The rule pinned here is the one that fails
/// silently in play: an IMGUI window clips what it draws, so a panel placed past the window's edge
/// loses the end of its sentence and nothing complains.
/// </summary>
static class TooltipPlacementTests
{
    const float W = 380f, H = 600f, M = 4f;

    static bool Inside((float x, float y) at, float w, float h) =>
        at.x >= M && at.y >= M && at.x + w <= W - M && at.y + h <= H - M;

    public static void Run()
    {
        var open = TooltipPlacement.Place(40f, 40f, 200f, 50f, W, H, M);
        Check.That(open.x > 40f && open.y > 40f, "with room, the panel sits right of and below the pointer");
        Check.That(Inside(open, 200f, 50f), "and inside the window");

        // The HUD's rows run to its right edge, so this is where the pointer usually is.
        var right = TooltipPlacement.Place(W - 20f, 100f, 200f, 50f, W, H, M);
        Check.That(right.x + 200f <= W - 20f, "near the right edge it flips to the left of the pointer");
        Check.That(Inside(right, 200f, 50f), "and stays inside");

        var bottom = TooltipPlacement.Place(100f, H - 10f, 200f, 50f, W, H, M);
        Check.That(bottom.y + 50f <= H - 10f, "near the bottom it flips above the pointer");
        Check.That(Inside(bottom, 200f, 50f), "and stays inside");

        var corner = TooltipPlacement.Place(W - 1f, H - 1f, 200f, 50f, W, H, M);
        Check.That(Inside(corner, 200f, 50f), "in the bottom-right corner it is still inside");

        var outside = TooltipPlacement.Place(-50f, -50f, 200f, 50f, W, H, M);
        Check.That(Inside(outside, 200f, 50f), "a pointer outside the window still places it inside");

        // Wider than the window: pinned to the top-left margin, so the START of the text shows.
        var wide = TooltipPlacement.Place(200f, 200f, W + 100f, 50f, W, H, M);
        Check.That(wide.x == M, "a panel wider than the window is pinned to the left margin");
    }
}
