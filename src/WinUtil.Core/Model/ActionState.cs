namespace WinUtil.Core.Model;

/// <summary>
/// The detected, ground-truth state of a tweak on the running system.
/// The UI binds to this — it never shows an inferred or remembered state.
/// </summary>
public enum ActionState
{
    Unknown = 0,

    /// <summary>Every declared change matches its target value on the system.</summary>
    Applied,

    /// <summary>Every declared change matches its original (or absent) value.</summary>
    NotApplied,

    /// <summary>The system matches neither pure state — e.g. a Windows update reverted part of an applied tweak.</summary>
    Drifted,
}
