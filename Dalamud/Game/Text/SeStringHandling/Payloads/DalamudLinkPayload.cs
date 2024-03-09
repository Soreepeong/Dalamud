using System.IO;

namespace Dalamud.Game.Text.SeStringHandling.Payloads;

/// <summary>
/// This class represents a custom Dalamud clickable chat link.
/// </summary>
public class DalamudLinkPayload : Payload
{
    /// <inheritdoc/>
    public override PayloadType Type => PayloadType.DalamudLink;

    /// <summary>
    /// Gets the plugin command ID to be linked.
    /// </summary>
    public uint CommandId { get; internal set; } = 0;

    /// <summary>
    /// Gets the plugin name to be linked.
    /// </summary>
    public string Plugin { get; internal set; } = string.Empty;

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{this.Type} -  Plugin: {this.Plugin}, Command: {this.CommandId}";
    }

    /// <inheritdoc/>
    protected override byte[] EncodeImpl()
    {
        var bodyLength = 0;
        bodyLength += SeStringExpressionUtilities.CalculateLengthInt((int)EmbeddedInfoType.DalamudLink - 1);
        bodyLength += SeStringExpressionUtilities.CalculateLengthString(this.Plugin);
        bodyLength += SeStringExpressionUtilities.CalculateLengthUInt(this.CommandId);

        var envelopeLength = 0;
        envelopeLength += 1; // START_BYTE
        envelopeLength += 1; // SeStringChunkType.Interactable
        envelopeLength += SeStringExpressionUtilities.CalculateLengthInt(bodyLength);
        envelopeLength += bodyLength;
        envelopeLength += 1; // END_BYTE;

        var buf = new byte[envelopeLength];
        var bufSpan = buf.AsSpan();
        bufSpan = SeStringExpressionUtilities.WriteRaw(bufSpan, START_BYTE);
        bufSpan = SeStringExpressionUtilities.WriteRaw(bufSpan, (byte)SeStringChunkType.Interactable);
        bufSpan = SeStringExpressionUtilities.EncodeInt(bufSpan, bodyLength);

        bufSpan = SeStringExpressionUtilities.EncodeInt(bufSpan, (int)EmbeddedInfoType.DalamudLink - 1);
        bufSpan = SeStringExpressionUtilities.EncodeString(bufSpan, this.Plugin);
        bufSpan = SeStringExpressionUtilities.EncodeUInt(bufSpan, this.CommandId);

        bufSpan = SeStringExpressionUtilities.WriteRaw(bufSpan, END_BYTE);

        if (!bufSpan.IsEmpty)
            throw new InvalidOperationException();

        return buf;
    }

    /// <inheritdoc/>
    protected override void DecodeImpl(BinaryReader reader, long endOfStream)
    {
        // Note: Payload.DecodeChunk already took the first int expr (DalamudLink).
        this.Plugin = GetStringAssumeUtf8Only(reader);
        this.CommandId = GetInteger(reader);
    }
}
