using Dalamud.Interface.SeStringRenderer.Internal;

using ImGuiNET;

namespace Dalamud.Interface.SeStringRenderer;

/// <summary>Parameters for <see cref="SeStringRenderer"/>.</summary>
public record struct SeStringRendererParams
{
    /// <summary>Describe a word break mode.</summary>
    public enum WordBreakType
    {
        /// <summary>Use the default line break rule.</summary>
        Normal,

        /// <summary>Insert word breaks between any two characters.</summary>
        BreakAll,

        /// <summary>Never break words.</summary>
        KeepAll,

        /// <summary>
        /// Insert word breaks between any two characters if the line still overflows under the default word break rule.
        /// </summary>
        BreakWord,
    }

    /// <summary>Specifies which line breaks should be honored.</summary>
    [Flags]
    public enum NewLineType : byte
    {
        /// <summary>Honor <c>\r</c> as a line break.</summary>
        Cr = 1,

        /// <summary>Honor <c>\n</c> as a line break.</summary>
        Lf = 2,

        /// <summary>Honor <c>\r\n</c> as a line break.</summary>
        CrLf = 4,

        /// <summary>Honor <see cref="MacroCode.NewLine"/> as a line break.</summary>
        SePayload = 8,

        /// <summary>Honor custom line breaks from <see cref="ISeStringRenderer.AddNewLine"/>, using this enumeration
        /// value as the parameter.</summary>
        Manual = 16,

        /// <summary>No line breaks will be honored.</summary>
        None = 0,

        /// <summary>Shortcut for all valid options.</summary>
        All = Cr | Lf | CrLf | SePayload | Manual,
    }

    /// <summary>Gets or sets the scaled font size.</summary>
    public float ScaledFontSize { get; set; }

    /// <summary>Gets or sets the scaled line height.</summary>
    public float ScaledLineHeight { get; set; }

    /// <summary>Gets or sets the vertical offset ratio.</summary>
    public float LineVerticalOffsetRatio { get; set; }

    /// <summary>Gets or sets the tab size.</summary>
    public float TabWidth { get; set; }

    /// <summary>Gets or sets the word break mode.</summary>
    public WordBreakType WordBreak { get; set; }

    /// <summary>Gets or sets horizontal offset at which point line break or ellipsis should happen.</summary>
    public float LineWrapWidth { get; set; }

    /// <summary>Gets or sets the ellipsis or line break indicator string to display.</summary>
    public string? WrapMarker { get; set; }

    /// <summary>Gets or sets the accepted line break types.</summary>
    public NewLineType AcceptedNewLines { get; set; }

    /// <summary>Gets or sets a value indicating whether to honor link payloads.</summary>
    public bool UseLink { get; set; }

    /// <summary>Gets or sets the graphic font icon mode.</summary>
    /// <remarks>
    /// <para><c>-1</c> will use the one configured from the game configuration.</para>
    /// <para>Numbers outside the supported range will roll over.</para>
    /// </remarks>
    public int GraphicFontIconMode { get; set; }

    /// <summary>Gets or sets the graphic font icon scale. </summary>
    public float GraphicFontIconScale { get; set; }

    /// <summary>Gets or sets the graphic font icon vertical offset ratio.</summary>
    public float GraphicFontIconVerticalOffsetRatio { get; set; }

    /// <summary>Gets or sets the design parameters for the SeString payload dumps, for debugging purposes.</summary>
    public SeStringRendererDesignParams? SeStringPayloadsDesignParams { get; set; }

    /// <summary>Gets or sets the design parameters for the invisible control characters dumps, for debugging purposes.
    /// </summary>
    public SeStringRendererDesignParams? ControlCharactersDesignParams { get; set; }

    /// <summary>Creates a new instance of <see cref="SeStringRendererParams"/> struct, using the current ImGui
    /// context and the recommended default values.</summary>
    /// <returns>A new instance of <see cref="SeStringRendererParams"/>.</returns>
    public static SeStringRendererParams FromCurrentImGuiContext() =>
        new()
        {
            ScaledFontSize = ImGui.GetFontSize(),
            ScaledLineHeight = ImGui.GetFontSize(),
            LineVerticalOffsetRatio = 1f,
            TabWidth = ImGui.CalcTextSize("    ").X,
            WordBreak = WordBreakType.KeepAll,
            LineWrapWidth = ImGui.GetContentRegionAvail().X,
            WrapMarker = "…",
            AcceptedNewLines = NewLineType.All,
            UseLink = true,
            GraphicFontIconMode = -1,
            SeStringPayloadsDesignParams = null,
            ControlCharactersDesignParams = null,
            GraphicFontIconScale = 1.25f,
            GraphicFontIconVerticalOffsetRatio = -0.05f,
        };
}
