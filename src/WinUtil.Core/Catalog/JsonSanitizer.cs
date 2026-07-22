using System.Text;

namespace WinUtil.Core.Catalog;

/// <summary>
/// winutil's catalogs are consumed by PowerShell's lenient ConvertFrom-Json and
/// contain raw control characters (tabs, newlines) inside string literals,
/// which strict JSON forbids. We consume the catalogs unchanged (ADR-0001), so
/// this pass escapes control characters found inside strings before parsing.
/// </summary>
public static class JsonSanitizer
{
    public static string Sanitize(string json)
    {
        var sb = new StringBuilder(json.Length);
        var inString = false;
        var escaped = false;

        foreach (var c in json)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                else if (c < 0x20)
                {
                    sb.Append(c switch
                    {
                        '\t' => "\\t",
                        '\n' => "\\n",
                        '\r' => "\\r",
                        _ => $"\\u{(int)c:x4}",
                    });
                    continue;
                }
            }
            else if (c == '"')
            {
                inString = true;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
