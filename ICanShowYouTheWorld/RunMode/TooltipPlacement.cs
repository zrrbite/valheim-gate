using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Where a hover tooltip goes inside a window: beside the pointer, flipped when that would run
    /// off the edge, and never outside the window at all.
    /// </summary>
    /// <remarks>
    /// Pure arithmetic in <c>RunMode/</c> so the one rule worth pinning - the panel stays inside the
    /// window's rect - is tested rather than eyeballed. It matters more than it looks: an IMGUI
    /// window CLIPS what it draws, so a tooltip placed past the edge is not "slightly off", it is
    /// half missing, and the half that is missing is the end of the sentence. Floats rather than
    /// Rect/Vector2 because this half of the mod is compiled without Unity by the test runner.
    /// </remarks>
    public static class TooltipPlacement
    {
        /// <summary>Gap between the pointer and the panel, so the pointer never covers the text.</summary>
        public const float PointerGap = 14f;

        /// <summary>
        /// The panel's top-left corner, in the same coordinates as <paramref name="mouseX"/>/<paramref name="mouseY"/>
        /// (the window's own). <paramref name="width"/> and <paramref name="height"/> are the panel's
        /// size and should already fit inside the window less its margins; a panel that does not
        /// fit is pinned to the top-left margin, which shows its beginning rather than its end.
        /// </summary>
        public static (float x, float y) Place(float mouseX, float mouseY, float width, float height,
                                               float windowWidth, float windowHeight, float margin = 4f)
        {
            // Right of and below the pointer, the way every desktop does it; to the LEFT when the
            // right would cross the edge - the HUD sits on the right of the screen and its rows
            // run to the window's edge, so this is the common case, not the corner case.
            float x = mouseX + PointerGap;
            if (x + width > windowWidth - margin) x = mouseX - PointerGap - width;

            float y = mouseY + PointerGap;
            if (y + height > windowHeight - margin) y = mouseY - PointerGap - height;

            x = Clamp(x, margin, windowWidth - margin - width);
            y = Clamp(y, margin, windowHeight - margin - height);
            return (x, y);
        }

        /// <summary>Lower bound wins when the range is inverted - a panel wider than the window.</summary>
        private static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(v, hi));
    }
}
