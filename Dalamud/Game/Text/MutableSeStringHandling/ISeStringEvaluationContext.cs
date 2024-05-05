using System.Numerics;

namespace Dalamud.Game.Text.MutableSeStringHandling;

/// <summary>Evaluation context for SeStrings.</summary>
public interface ISeStringEvaluationContext
{
    /// <summary>Gets or sets the contextual time.</summary>
    DateTime ContextualTime { get; set; }
    
    /// <summary>Gets or sets the wait time after evaluating the whole SeString.</summary>
    TimeSpan WaitTime { get; set; }

    /// <summary>Gets or sets the foreground text color.</summary>
    uint ForeColor { get; set; }
    
    /// <summary>Gets or sets the text edge glow color.</summary>
    uint EdgeColor { get; set; }
    
    /// <summary>Gets or sets the text shadow color.</summary>
    uint ShadowColor { get; set; }
    
    /// <summary>Gets or sets a value indicating whether to make any following text bold.</summary>
    bool Bold { get; set; }
    
    /// <summary>Gets or sets a value indicating whether to italicize any following text.</summary>
    bool Italicize { get; set; }

    /// <summary>Gets the local number.</summary>
    /// <param name="index">Index of the local number.</param>
    /// <returns>The local number.</returns>
    /// <remarks>Return <c>0</c> if none is available.</remarks>
    int GetLocalNumber(int index);

    /// <summary>Sets the local number.</summary>
    /// <param name="index">Index of the local number.</param>
    /// <param name="value">New value of the local number.</param>
    void SetLocalNumber(int index, int value);

    /// <summary>Gets the local string.</summary>
    /// <param name="index">Index of the local string.</param>
    /// <returns>The local string.</returns>
    /// <remarks>Return <c>null</c> if none is available.</remarks>
    MutableSeString? GetLocalString(int index);

    /// <summary>Sets the local string.</summary>
    /// <param name="index">Index of the local string.</param>
    /// <param name="value">New value of the local string.</param>
    void SetLocalString(int index, MutableSeString? value);

    /// <summary>Gets the global number.</summary>
    /// <param name="index">Index of the global number.</param>
    /// <returns>The global number.</returns>
    /// <remarks>Return <c>0</c> if none is available.</remarks>
    int GetGlobalNumber(int index);

    /// <summary>Sets the global number.</summary>
    /// <param name="index">Index of the global number.</param>
    /// <param name="value">New value of the global number.</param>
    void SetGlobalNumber(int index, int value);

    /// <summary>Gets the global string.</summary>
    /// <param name="index">Index of the global string.</param>
    /// <returns>The global string.</returns>
    /// <remarks>Return <c>null</c> if none is available.</remarks>
    MutableSeString? GetGlobalString(int index);

    /// <summary>Sets the global string.</summary>
    /// <param name="index">Index of the global string.</param>
    /// <param name="value">New value of the global string.</param>
    void SetGlobalString(int index, MutableSeString? value);

    /// <summary>Pushes a color to the color stack.</summary>
    /// <param name="color">Color value in RGBA32.</param>
    void PushColor(uint color);

    /// <summary>Peeks a color from the color stack.</summary>
    /// <param name="color">Color value at the top of the stack.</param>
    /// <returns><c>true</c> if the stack is not empty.</returns>
    bool TryPeekColor(out uint color);

    /// <summary>Pops a color from the color stack.</summary>
    /// <param name="color">Color value at the top of the stack.</param>
    /// <returns><c>true</c> if the stack is not empty.</returns>
    bool TryPopColor(out uint color);

    /// <summary>Gets the local player ID.</summary>
    /// <returns>The local player ID.</returns>
    /// <value>Use 0xE0000000 if none is available.</value>
    int GetLocalPlayerId();

    /// <summary>Gets the name of a game object.</summary>
    /// <param name="objectId">ID of the game object.</param>
    /// <returns>Name of the game object, or <c>null</c> if not available.</returns>
    MutableSeString? GetObjectName(int objectId);
    
    /// <summary>Gets a value indicating whether the object is male.</summary>
    /// <param name="objectId">ID of the game object.</param>
    /// <returns><c>true</c> if the object is male.</returns>
    bool IsObjectMale(int objectId);
}
