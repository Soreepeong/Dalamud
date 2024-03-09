using System.IO;

using Microsoft.Extensions.ObjectPool;

namespace Dalamud.Data.SeStringEvaluation.SeStringContext.Internal;

/// <summary>A policy for pooling <see cref="MemoryStream"/> instances.</summary>
internal class MemoryStreamObjectPoolPolicy : PooledObjectPolicy<MemoryStream>
{
    /// <summary>Gets or sets the initial capacity of pooled <see cref="MemoryStream"/> instances.</summary>
    /// <value>Defaults to <c>100</c>.</value>
    public int InitialCapacity { get; set; } = 1024;

    /// <summary>Gets or sets the maximum value for <see cref="MemoryStream.Capacity"/> that is allowed to be
    /// retained, when <see cref="Return(MemoryStream)"/> is invoked.</summary>
    /// <value>Defaults to <c>4096</c>.</value>
    public int MaximumRetainedCapacity { get; set; } = 4 * 1024;

    /// <inheritdoc />
    public override MemoryStream Create() => new(this.InitialCapacity);

    /// <inheritdoc />
    public override bool Return(MemoryStream obj)
    {
        if (obj.Capacity > this.MaximumRetainedCapacity)
        {
            // Too big. Discard this one.
            return false;
        }

        obj.Position = 0;
        obj.SetLength(0);
        return true;
    }
}
