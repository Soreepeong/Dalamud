using System.Collections.Generic;
using System.Linq;

using Dalamud.Game.Text.SeStringHandling;

using ImGuiNET;

namespace Dalamud.Interface.Internal.Windows.Data.Widgets;

/// <summary>
/// Widget for displaying clipboard info.
/// </summary>
internal class ClipboardChangelogWidget : IDataWindowWidget, IDisposable
{
    private readonly List<(
        SeString Str,
        string Repr,
        IClipboardProvider.ClipboardChangeSource Source)> history = new();
    
    private int selectedItemIndex = 0;

    /// <inheritdoc/>
    public string[]? CommandShortcuts { get; init; } = { "clipboardchangelog" };

    /// <inheritdoc/>
    public string DisplayName { get; init; } = "Clipboard Changelog";

    /// <inheritdoc/>
    public bool Ready { get; set; }

    /// <inheritdoc/>
    public void Load()
    {
        if (this.Ready)
            return;
        Service<ClipboardProvider>.GetAsync().ContinueWith(r => r.Result.ClipboardChange += this.OnClipboardChange);
        this.Ready = true;
    }

    /// <inheritdoc/>
    public void Draw()
    {
        var prov = Service<ClipboardProvider>.Get();

        if (ImGui.Button("Clear history"))
        {
            this.history.Clear();
            this.selectedItemIndex = 0;
        }

        if (ImGui.BeginListBox("History"))
        {
            for (var i = 0; i < this.history.Count; i++)
            {
                if (ImGui.Selectable(this.history[i].Repr, this.selectedItemIndex == i))
                {
                    this.selectedItemIndex = i;
                    prov.SetClipboardSeString(this.history[i].Str);
                }
            }

            ImGui.EndListBox();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.Ready)
            return;
        Service<ClipboardProvider>.GetAsync().ContinueWith(r => r.Result.ClipboardChange -= this.OnClipboardChange);
        this.Ready = false;
    }

    private void OnClipboardChange(IClipboardProvider.ClipboardChangeSource source)
    {
        var sestr = Service<ClipboardProvider>.Get().GetClipboardSeString();
        if (this.selectedItemIndex >= 0
            && this.selectedItemIndex < this.history.Count
            && sestr.Encode().AsSpan().SequenceEqual(this.history[this.selectedItemIndex].Str.Encode().AsSpan()))
        {
            return;
        }

        var repr = $"[{source}] " + string.Join(
                       null,
                       sestr.Payloads.Select(
                           x => x is ITextProvider itp ? itp.Text : $"<{x.GetType()}({x})>"));
        this.history.Insert(0, (sestr, repr, source));
        if (this.history.Count > 100)
            this.history.RemoveAt(this.history.Count - 1);
        else
            this.selectedItemIndex++;
    }
}
