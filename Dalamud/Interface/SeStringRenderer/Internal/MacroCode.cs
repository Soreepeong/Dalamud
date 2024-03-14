#pragma warning disable

namespace Dalamud.Interface.SeStringRenderer.Internal;

// Copied from Lumina
internal enum MacroCode : byte
{
    SetResetTime = 0x06, // n N x
    SetTime = 0x07, // n x
    If = 0x08, // . . * x
    Switch = 0x09, // . . .
    PcName = 0x0A, // n x
    IfPcGender = 0x0B, // n . . x
    IfPcName = 0x0C, // n . . . x
    Josa = 0x0D, // s s s x
    Josaro = 0x0E, // s s s x
    IfSelf = 0x0F, // n . . x
    NewLine = 0x10, // <br>
    Wait = 0x11, // n x
    Icon = 0x12, // n x
    Color = 0x13, // n N x
    EdgeColor = 0x14, // n N x
    ShadowColor = 0x15, // n N x
    SoftHyphen = 0x16, // -
    Key = 0x17, // 
    Scale = 0x18, // n
    Bold = 0x19, // n
    Italic = 0x1A, // n
    Edge = 0x1B, // n n
    Shadow = 0x1C, // n n
    NonBreakingSpace = 0x1D, // <nbsp>
    Icon2 = 0x1E, // n N x
    Hyphen = 0x1F, // --
    Num = 0x20, // n x
    Hex = 0x21, // n x
    Kilo = 0x22, // . s x
    Byte = 0x23, // n x
    Sec = 0x24, // n x
    Time = 0x25, // n x
    Float = 0x26, // n n s x
    Link = 0x27, // n n n n s
    Sheet = 0x28, // s . . .
    String = 0x29, // s x
    Caps = 0x2A, // s x
    Head = 0x2B, // s x
    Split = 0x2C, // s s n x
    HeadAll = 0x2D, // s x
    Fixed = 0x2E, // n n . . .
    Lower = 0x2F, // s x
    JaNoun = 0x30, // s . .
    EnNoun = 0x31, // s . .
    DeNoun = 0x32, // s . .
    FrNoun = 0x33, // s . .
    ChNoun = 0x34, // s . .
    LowerHead = 0x40, // s x
    ColorType = 0x48, // n x
    EdgeColorType = 0x49, // n x
    Digit = 0x50, // n n x
    Ordinal = 0x51, // n x
    Sound = 0x60, // n n
    LevelPos = 0x61 // n x
}
