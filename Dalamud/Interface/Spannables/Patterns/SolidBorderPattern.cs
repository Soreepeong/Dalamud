using Dalamud.Interface.Spannables.EventHandlerArgs;
using Dalamud.Interface.Spannables.Helpers;
using Dalamud.Interface.Spannables.Rendering;
using Dalamud.Utility;

using ImGuiNET;

namespace Dalamud.Interface.Spannables.Patterns;

/// <summary>A spannable that renders a solid colored border.</summary>
public sealed class SolidBorderPattern : PatternSpannable
{
    /// <summary>Gets or sets the channel to render to.</summary>
    public RenderChannel TargetChannel { get; set; } = RenderChannel.BackChannel;

    /// <summary>Gets or sets the fill color.</summary>
    public Rgba32 Color { get; set; }

    /// <summary>Gets or sets the thickness.</summary>
    public float Thickness { get; set; } = 1;

    /// <summary>Gets or sets the rounding.</summary>
    public float Rounding { get; set; } = 0;

    /// <summary>Gets or sets the rounding flags.</summary>
    public ImDrawFlags RoundingFlags { get; set; } = ImDrawFlags.RoundCornersDefault;

    /// <inheritdoc/>
    protected override PatternRenderPass CreateNewRenderPass() => new SolidBorderRenderPass(this);

    /// <summary>A state for <see cref="LayeredPattern"/>.</summary>
    private class SolidBorderRenderPass(SolidBorderPattern owner) : PatternRenderPass
    {
        public override void DrawSpannable(SpannableDrawArgs args)
        {
            base.DrawSpannable(args);

            var lt = args.RenderPass.Boundary.LeftTop;
            var rb = args.RenderPass.Boundary.RightBottom;

            args.SwitchToChannel(owner.TargetChannel);

            using var st = ScopedTransformer.From(args, 1f);
            args.DrawListPtr.AddRect(
                lt,
                rb,
                owner.Color,
                owner.Rounding,
                owner.RoundingFlags,
                owner.Thickness);
        }
    }
}
