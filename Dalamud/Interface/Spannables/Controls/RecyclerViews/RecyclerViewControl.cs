using System.Collections;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Interface.Spannables.EventHandlers;
using Dalamud.Interface.Spannables.Patterns;
using Dalamud.Interface.Spannables.Styles;
using Dalamud.Utility.Numerics;

using ImGuiNET;

namespace Dalamud.Interface.Spannables.Controls.RecyclerViews;

#pragma warning disable SA1010

/// <summary>A recycler view control, which is a base for list views and grid views.</summary>
// TODO: should this control manage header area?
public abstract partial class RecyclerViewControl : ControlSpannable
{
    /// <summary>A sentinel value that indicates that the spannable type is invalid (could not be fetched.)</summary>
    public const int InvalidSpannableType = -1;

    private readonly Dictionary<int, List<Spannable>> placeholders = new();

    private BaseLayoutManager? layoutManager;
    private Vector2 autoScrollPerSecond;
    private ScrollBarMode verticalScrollBarMode;
    private ScrollBarMode horizontalScrollBarMode;

    /// <summary>Initializes a new instance of the <see cref="RecyclerViewControl"/> class.</summary>
    protected RecyclerViewControl()
    {
        this.ClipChildren = true;
        this.AddChild(
            this.VerticalScrollBar = new()
            {
                Name = nameof(this.VerticalScrollBar),
                Direction = LinearDirection.TopToBottom,
                Size = new(WrapContent, MatchParent),
                AutoValueUpdate = false,
                OccupySpaceWhenHidden = false,
                ZOrder = 100,
            });
        this.AddChild(
            this.HorizontalScrollBar = new()
            {
                Name = nameof(this.HorizontalScrollBar),
                Direction = LinearDirection.LeftToRight,
                Size = new(MatchParent, WrapContent),
                AutoValueUpdate = false,
                OccupySpaceWhenHidden = false,
                ZOrder = 100,
            });
        this.AddChild(
            this.ScrollBarIntersectionDummy = new()
            {
                Type = ShapePattern.Shape.RectFilled,
                ImGuiColor = ImGuiCol.WindowBg,
                CaptureMouseOnMouseDown = true,
                OccupySpaceWhenHidden = false,
                ZOrder = 100,
            });
        this.VerticalScrollBar.PropertyChange += this.ChildOnPropertyChange;
        this.HorizontalScrollBar.PropertyChange += this.ChildOnPropertyChange;
    }

    /// <summary>Delegate for <see cref="DecideSpannableType"/>.</summary>
    /// <param name="args">The arguments.</param>
    public delegate void DecideSpannableTypeEventDelegate(DecideSpannableTypeEventArg args);

    /// <summary>Delegate for <see cref="AddMoreSpannables"/>.</summary>
    /// <param name="args">The arguments.</param>
    public delegate void AddMoreSpannablesEventDelegate(AddMoreSpannablesEventArg args);

    /// <summary>Delegate for <see cref="PopulateSpannable"/>.</summary>
    /// <param name="args">The arguments.</param>
    public delegate void PopulateSpannableEventDelegate(PopulateSpannableEventArg args);

    /// <summary>Delegate for <see cref="PopulateSpannable"/>.</summary>
    /// <param name="args">The arguments.</param>
    public delegate void ClearSpannableEventDelegate(ClearSpannableEventArg args);

    /// <summary>Occurs when the type of spannable at a given index needs to be decided.</summary>
    public event DecideSpannableTypeEventDelegate? DecideSpannableType;

    /// <summary>Occurs when more spannables of the given the type are in need.</summary>
    /// <remarks>Call <see cref="AddPlaceholder"/> during this event.</remarks>
    public event AddMoreSpannablesEventDelegate? AddMoreSpannables;

    /// <summary>Occurs when the spannable should be populated from the data.</summary>
    public event PopulateSpannableEventDelegate? PopulateSpannable;

    /// <summary>Occurs when the spannable should be cleared.</summary>
    public event ClearSpannableEventDelegate? ClearSpannable;

    /// <summary>Occurs when the list has been scrolled.</summary>
    public event SpannableEventHandler? Scroll;

    /// <summary>Occurs when the property <see cref="LayoutManager"/> is changing.</summary>
    public event PropertyChangeEventHandler<BaseLayoutManager?>? LayoutManagerChange;

    /// <summary>Occurs when the property <see cref="AutoScrollPerSecond"/> is changing.</summary>
    public event PropertyChangeEventHandler<Vector2>? AutoScrollPerSecondChange;

    /// <summary>Occurs when the property <see cref="HorizontalScrollBarMode"/> is changing.</summary>
    public event PropertyChangeEventHandler<ScrollBarMode>? HorizontalScrollBarModeChange;

    /// <summary>Occurs when the property <see cref="VerticalScrollBarMode"/> is changing.</summary>
    public event PropertyChangeEventHandler<ScrollBarMode>? VerticalScrollBarModeChange;

    /// <summary>Gets or sets the layout manager.</summary>
    public BaseLayoutManager? LayoutManager
    {
        get => this.layoutManager;
        set => this.HandlePropertyChange(
            nameof(this.LayoutManager),
            ref this.layoutManager,
            value,
            this.layoutManager == value,
            this.OnLayoutManagerChange);
    }

    /// <summary>Gets or sets the auto scroll speed, in terms of amount of lines scrolled from a single detent in mouse
    /// wheels per every second.</summary>
    public Vector2 AutoScrollPerSecond
    {
        get => this.autoScrollPerSecond;
        set => this.HandlePropertyChange(
            nameof(this.AutoScrollPerSecond),
            ref this.autoScrollPerSecond,
            value,
            this.autoScrollPerSecond == value,
            this.OnAutoScrollPerSecondChange);
    }

    /// <summary>Gets or sets when to display the vertical scroll bar.</summary>
    public ScrollBarMode VerticalScrollBarMode
    {
        get => this.verticalScrollBarMode;
        set => this.HandlePropertyChange(
            nameof(this.VerticalScrollBarMode),
            ref this.verticalScrollBarMode,
            value,
            this.verticalScrollBarMode == value,
            this.OnVerticalScrollBarModeChange);
    }

    /// <summary>Gets or sets when to display the horizontal scroll bar.</summary>
    public ScrollBarMode HorizontalScrollBarMode
    {
        get => this.horizontalScrollBarMode;
        set => this.HandlePropertyChange(
            nameof(this.HorizontalScrollBarMode),
            ref this.horizontalScrollBarMode,
            value,
            this.horizontalScrollBarMode == value,
            this.OnHorizontalScrollBarModeChange);
    }

    /// <inheritdoc/>
    public override bool IsAnyAnimationRunning =>
        base.IsAnyAnimationRunning
        || this.layoutManager?.IsAnyAnimationRunning is true;

    /// <summary>Gets the vertical scroll bar.</summary>
    public ScrollBarControl VerticalScrollBar { get; }

    /// <summary>Gets the horizontal scroll bar.</summary>
    public ScrollBarControl HorizontalScrollBar { get; }

    /// <summary>Gets the dummy for covering up the rectangle between scrollbars if they're both visible.</summary>
    public ShapePattern ScrollBarIntersectionDummy { get; }

    /// <summary>Adds a placeholder for use.</summary>
    /// <param name="spannableType">Spannable type of the placeholder.</param>
    /// <param name="spannable">New placeholder spannable.</param>
    public void AddPlaceholder(int spannableType, Spannable spannable)
    {
        if (spannable is null)
            throw new NullReferenceException();

        if (!this.placeholders.TryGetValue(spannableType, out var plist))
            this.placeholders.Add(spannableType, plist = []);
        plist.Add(spannable);
        spannable.PropertyChange += this.ChildOnPropertyChange;
    }

    /// <inheritdoc/>
    protected override void OnPreDispatchEvents(SpannableEventArgs args)
    {
        this.layoutManager?.PreDispatchEvents();
        base.OnPreDispatchEvents(args);
    }

    /// <summary>Gets the underlying list.</summary>
    /// <returns>The list, or <c>null</c> if no list is bound.</returns>
    protected abstract ICollection? GetCollection();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            (this.layoutManager as IProtectedLayoutManager)?.SetRecyclerView(null);
            foreach (var v in this.placeholders.Values)
            {
                foreach (var v2 in v)
                    v2.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override RectVector4 MeasureContentBox(Vector2 suggestedSize)
    {
        this.Padding = this.Padding with { Bottom = this.HorizontalScrollBar.Boundary.Bottom };
        this.HorizontalScrollBar.RenderPassMeasure(suggestedSize with { Y = float.PositiveInfinity });

        this.Padding = this.Padding with { Right = this.VerticalScrollBar.Boundary.Right };
        this.VerticalScrollBar.RenderPassMeasure(suggestedSize with { X = float.PositiveInfinity, });

        this.ScrollBarIntersectionDummy.RenderPassMeasure(
            new(
                this.VerticalScrollBar.Boundary.Width,
                this.HorizontalScrollBar.Boundary.Height));

        return this.layoutManager?.MeasureContentBox(suggestedSize) ?? base.MeasureContentBox(suggestedSize);
    }

    /// <inheritdoc/>
    protected override void OnPlace(SpannableEventArgs args)
    {
        this.layoutManager?.UpdateTransformation();

        this.HorizontalScrollBar.RenderPassPlace(
            Matrix4x4.CreateTranslation(new(this.MeasuredContentBox.LeftBottom, 0)),
            this.FullTransformation);
        this.VerticalScrollBar.RenderPassPlace(
            Matrix4x4.CreateTranslation(new(this.MeasuredContentBox.RightTop, 0)),
            this.FullTransformation);
        this.ScrollBarIntersectionDummy.RenderPassPlace(
            Matrix4x4.CreateTranslation(new(this.MeasuredContentBox.RightBottom, 0)),
            this.FullTransformation);

        base.OnPlace(args);
    }

    /// <inheritdoc/>
    protected override void OnDrawInside(SpannableDrawEventArgs args)
    {
        this.layoutManager?.Draw(args);

        this.HorizontalScrollBar.RenderPassDraw(args.DrawListPtr);
        this.VerticalScrollBar.RenderPassDraw(args.DrawListPtr);
        this.ScrollBarIntersectionDummy.RenderPassDraw(args.DrawListPtr);

        base.OnDrawInside(args);
    }

    /// <summary>Raises the <see cref="DecideSpannableType"/> event.</summary>
    /// <param name="args">A <see cref="DecideSpannableTypeEventArg"/> that contains the event data.</param>
    protected virtual void OnDecideSpannableType(DecideSpannableTypeEventArg args) =>
        this.DecideSpannableType?.Invoke(args);

    /// <summary>Raises the <see cref="AddMoreSpannables"/> event.</summary>
    /// <param name="args">A <see cref="AddMoreSpannablesEventArg"/> that contains the event data.</param>
    protected virtual void OnAddMoreSpannables(AddMoreSpannablesEventArg args) =>
        this.AddMoreSpannables?.Invoke(args);

    /// <summary>Raises the <see cref="PopulateSpannable"/> event.</summary>
    /// <param name="args">A <see cref="PopulateSpannableEventArg"/> that contains the event data.</param>
    protected virtual void OnPopulateSpannable(PopulateSpannableEventArg args) =>
        this.PopulateSpannable?.Invoke(args);

    /// <summary>Raises the <see cref="ClearSpannable"/> event.</summary>
    /// <param name="args">A <see cref="ClearSpannableEventArg"/> that contains the event data.</param>
    protected virtual void OnClearSpannable(ClearSpannableEventArg args) => this.ClearSpannable?.Invoke(args);

    /// <summary>Raises the <see cref="Scroll"/> event.</summary>
    /// <param name="args">A <see cref="SpannableEventArgs"/> that contains the event data.</param>
    protected virtual void OnScroll(SpannableEventArgs args) => this.Scroll?.Invoke(args);

    /// <summary>Raises the <see cref="LayoutManagerChange"/> event.</summary>
    /// <param name="args">A <see cref="PropertyChangeEventArgs{T}"/> that contains the event data.</param>
    protected virtual void OnLayoutManagerChange(PropertyChangeEventArgs<BaseLayoutManager?> args) =>
        this.LayoutManagerChange?.Invoke(args);

    /// <summary>Raises the <see cref="AutoScrollPerSecondChange"/> event.</summary>
    /// <param name="args">A <see cref="PropertyChangeEventArgs{T}"/> that contains the event data.</param>
    protected virtual void OnAutoScrollPerSecondChange(PropertyChangeEventArgs<Vector2> args) =>
        this.AutoScrollPerSecondChange?.Invoke(args);

    /// <summary>Raises the <see cref="HorizontalScrollBarModeChange"/> event.</summary>
    /// <param name="args">A <see cref="PropertyChangeEventArgs{T}"/> that contains the event data.</param>
    protected virtual void OnHorizontalScrollBarModeChange(PropertyChangeEventArgs<ScrollBarMode> args) =>
        this.HorizontalScrollBarModeChange?.Invoke(args);

    /// <summary>Raises the <see cref="VerticalScrollBarModeChange"/> event.</summary>
    /// <param name="args">A <see cref="PropertyChangeEventArgs{T}"/> that contains the event data.</param>
    protected virtual void OnVerticalScrollBarModeChange(PropertyChangeEventArgs<ScrollBarMode> args) =>
        this.VerticalScrollBarModeChange?.Invoke(args);

    /// <inheritdoc/>
    protected override void OnMouseWheel(SpannableMouseEventArgs args)
    {
        base.OnMouseWheel(args);
        if ((!this.VerticalScrollBar.Visible && !this.HorizontalScrollBar.Visible)
            || args.SuppressHandling
            || !this.IsMouseHoveredIncludingChildren
            || this.layoutManager is null)
            return;

        args.SuppressHandling = true;

        this.layoutManager.SmoothScrollByLines(-args.WheelDelta);
    }

    private void ChildOnPropertyChange(PropertyChangeEventArgs args) => this.RequestMeasure();

    public record DecideSpannableTypeEventArg : SpannableEventArgs
    {
        /// <summary>Gets the index of the item that needs to have its spannable type decided.</summary>
        public int Index { get; private set; }

        /// <summary>Gets or sets the decided spannable type.</summary>
        /// <remarks>Assign to this property to assign a spannable type.</remarks>
        public int SpannableType { get; set; }

        /// <summary>Gets or sets the decided spannable type for decoration.</summary>
        /// <remarks>This is used as the background which does not get scrolled in non-main direction.
        /// Leave it as <see cref="RecyclerViewControl.InvalidSpannableType"/> to disable.</remarks>
        public int DecorationType { get; set; }

        /// <summary>Initializes the direct properties of <see cref="DecideSpannableTypeEventArg"/>.</summary>
        /// <param name="index">Index.</param>
        public void InitializeDecideSpannableType(int index)
        {
            this.Index = index;
            this.SpannableType = InvalidSpannableType;
            this.DecorationType = InvalidSpannableType;
        }
    }

    public record AddMoreSpannablesEventArg : SpannableEventArgs
    {
        /// <summary>Gets the type of the spannable that needs to be populated.</summary>
        public int SpannableType { get; private set; }

        /// <summary>Initializes the direct properties of <see cref="AddMoreSpannablesEventArg"/>.</summary>
        /// <param name="spannableType">The spannable type.</param>
        public void InitializeAddMoreSpannables(int spannableType) => this.SpannableType = spannableType;
    }

    public record PopulateSpannableEventArg : SpannableEventArgs
    {
        /// <summary>Gets the index of the item that needs to have its spannable type decided.</summary>
        public int Index { get; private set; }

        /// <summary>Gets the decided spannable type from <see cref="DecideSpannableType"/>.</summary>
        public int SpannableType { get; private set; }

        /// <summary>Gets the associated spannable measurement.</summary>
        public Spannable Spannable { get; private set; } = null!;

        /// <inheritdoc/>
        public override bool TryReset()
        {
            this.Spannable = null!;
            return base.TryReset();
        }

        /// <summary>Initializes the direct properties of <see cref="PopulateSpannableEventArg"/>.</summary>
        /// <param name="index">Index.</param>
        /// <param name="spannableType">Spannable type.</param>
        /// <param name="spannable">Spannable.</param>
        public void InitializePopulateSpannable(int index, int spannableType, Spannable spannable)
        {
            this.Index = index;
            this.SpannableType = spannableType;
            this.Spannable = spannable;
        }
    }

    public record ClearSpannableEventArg : SpannableEventArgs
    {
        /// <summary>Gets the decided spannable type from <see cref="DecideSpannableType"/>.</summary>
        public int SpannableType { get; private set; }

        /// <summary>Gets the associated spannable measurement.</summary>
        public Spannable Spannable { get; private set; } = null!;

        /// <inheritdoc/>
        public override bool TryReset()
        {
            this.Spannable = null!;
            return base.TryReset();
        }

        /// <summary>Initializes the direct properties of <see cref="ClearSpannableEventArg"/>.</summary>
        /// <param name="spannableType">Spannable type.</param>
        /// <param name="spannable">Spannable.</param>
        public void InitializeClearSpannable(int spannableType, Spannable spannable)
        {
            this.SpannableType = spannableType;
            this.Spannable = spannable;
        }
    }
}