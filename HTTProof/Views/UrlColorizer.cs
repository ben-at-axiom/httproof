using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace HTTProof.Views;

public sealed class UrlColorizer : DocumentColorizingTransformer
{
    private static readonly Regex SchemeRegex = new(@"^([a-zA-Z][a-zA-Z0-9+.\-]*)://", RegexOptions.Compiled);

    private static readonly IBrush SchemeBrush   = new SolidColorBrush(Color.Parse("#4EC9B0"));
    private static readonly IBrush MutedBrush    = new SolidColorBrush(Color.Parse("#8A93A6"));
    private static readonly IBrush HostBrush     = new SolidColorBrush(Color.Parse("#DCDCAA"));
    private static readonly IBrush PortBrush     = new SolidColorBrush(Color.Parse("#B5CEA8"));
    private static readonly IBrush PathBrush     = new SolidColorBrush(Color.Parse("#E6EAF2"));
    private static readonly IBrush QueryKeyBrush = new SolidColorBrush(Color.Parse("#9CDCFE"));
    private static readonly IBrush QueryValBrush = new SolidColorBrush(Color.Parse("#CE9178"));
    private static readonly IBrush FragBrush     = new SolidColorBrush(Color.Parse("#C586C0"));

    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.Length == 0) return;
        var text = CurrentContext.Document.GetText(line);
        int start = line.Offset;
        int i = 0;

        var sm = SchemeRegex.Match(text);
        if (sm.Success)
        {
            var g = sm.Groups[1];
            Paint(start + g.Index, g.Length, SchemeBrush);
            Paint(start + g.Index + g.Length, 3, MutedBrush);
            i = sm.Length;
        }

        int hostEnd = i;
        while (hostEnd < text.Length && "/?#".IndexOf(text[hostEnd]) < 0) hostEnd++;
        if (hostEnd > i)
        {
            int colon = text.IndexOf(':', i, hostEnd - i);
            if (colon >= 0)
            {
                Paint(start + i, colon - i, HostBrush);
                Paint(start + colon, hostEnd - colon, PortBrush);
            }
            else
            {
                Paint(start + i, hostEnd - i, HostBrush);
            }
            i = hostEnd;
        }

        int pathEnd = i;
        while (pathEnd < text.Length && "?#".IndexOf(text[pathEnd]) < 0) pathEnd++;
        for (int k = i; k < pathEnd; k++)
            Paint(start + k, 1, text[k] == '/' ? MutedBrush : PathBrush);
        i = pathEnd;

        if (i < text.Length && text[i] == '?')
        {
            int qEnd = text.IndexOf('#', i);
            if (qEnd < 0) qEnd = text.Length;
            Paint(start + i, 1, MutedBrush);
            int c = i + 1;
            while (c < qEnd)
            {
                int amp = text.IndexOf('&', c);
                int end = (amp < 0 || amp >= qEnd) ? qEnd : amp;
                int eq = text.IndexOf('=', c);
                if (eq >= c && eq < end)
                {
                    Paint(start + c, eq - c, QueryKeyBrush);
                    Paint(start + eq, 1, MutedBrush);
                    Paint(start + eq + 1, end - eq - 1, QueryValBrush);
                }
                else
                {
                    Paint(start + c, end - c, QueryKeyBrush);
                }
                if (amp < 0 || amp >= qEnd) break;
                Paint(start + amp, 1, MutedBrush);
                c = amp + 1;
            }
            i = qEnd;
        }

        if (i < text.Length && text[i] == '#')
            Paint(start + i, text.Length - i, FragBrush);
    }

    private void Paint(int start, int len, IBrush brush)
    {
        if (len <= 0) return;
        ChangeLinePart(start, start + len, e => e.TextRunProperties.SetForegroundBrush(brush));
    }
}
