namespace BlePositioning.Application.Floors;

/// <summary>与 <c>alert_rules.trigger_on</c> 对应的业务含义（<see cref="short"/> 值）。</summary>
public enum AlertTriggerKind : short
{
    /// <summary>由区外进入多边形时触发（阶段 C 将消费）。</summary>
    Enter = 0,

    /// <summary>由区内离开多边形时触发。</summary>
    Exit = 1,

    /// <summary>进入与离开均可能触发（进入/离开各判一次，实现细节见领域逻辑）。</summary>
    EnterOrExit = 2,
}
