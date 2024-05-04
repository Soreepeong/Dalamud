namespace Dalamud.Game.Text.MutableSeStringHandling;

public interface ISeStringEvaluationContext
{
    DateTime ContextualTime { get; set; }
    
    TimeSpan WaitTime { get; set; }

    uint ForeColor { get; set; }
    
    uint EdgeColor { get; set; }
    
    uint ShadowColor { get; set; }
    
    bool Bold { get; set; }
    
    bool Italicize { get; set; }

    int GetLocalNumber(int index, int defaultValue = 0);

    void SetLocalNumber(int index, int value);

    int GetGlobalNumber(int index, int defaultValue = 0);

    void SetGlobalNumber(int index, int value);

    MutableSeString? GetLocalString(int index);

    void SetLocalString(int index, MutableSeString? value);

    MutableSeString? GetGlobalString(int index);

    void SetGlobalString(int index, MutableSeString? value);

    void PushColor(uint rgba32);

    bool TryPeekColor(out uint rgba32);

    bool TryPopColor(out uint rgba32);

    int GetLocalPlayerId();

    MutableSeString? GetObjectName(int objectId);
    
    bool IsObjectMale(int objectId);
}
