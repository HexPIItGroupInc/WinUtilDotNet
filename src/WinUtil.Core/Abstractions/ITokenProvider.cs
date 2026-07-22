namespace WinUtil.Core.Abstractions;

/// <summary>
/// Resolves {{TOKEN}} placeholders in overlay values that depend on the live
/// machine — e.g. {{RAM_KB}} (physical memory in KB) or {{USER_SID}}. The
/// Windows adapter computes them; Core never queries the machine directly.
/// </summary>
public interface ITokenProvider
{
    /// <summary>Replace every {{TOKEN}} in the text. Unknown tokens throw.</summary>
    string Resolve(string text);
}
