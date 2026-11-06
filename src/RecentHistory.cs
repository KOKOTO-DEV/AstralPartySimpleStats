using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AstralPartySimpleStats;

internal sealed class RecentHistory
{
    public bool Public { get; }
    public IReadOnlyList<string> Rows { get; }
    public string Summary => !Public ? "최근: 비공개" : Rows.Count == 0 ? "최근: 기록 없음"
        : "최근: " + string.Concat(Rows.Select(x => x.Split('\t')[1]=="승"?"승":"패")) + " (" + Rows.Count(x=>x.Split('\t')[1]=="승") + "/" + Rows.Count + ")";
    private RecentHistory(bool visible, string[] rows) { Public=visible; Rows=rows; }
    public string Column(int start,bool useNames=false) => string.Join("\n", Rows.Skip(start).Take(5).Select((r,i) =>
    {
        var p=r.Split('\t');
        return (start+i+1).ToString("00",System.Globalization.CultureInfo.InvariantCulture)+". "+p[0]+" "+p[1]+" "+(useNames && p.Length==4?p[3]:p[2]);
    }));
    public static RecentHistory? Parse(string visible, string encoded)
    {
        if (visible=="0") return new RecentHistory(false,Array.Empty<string>());
        if (visible!="1" || encoded.Length>32768) return null;
        try
        {
            var rows=Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Split('\n',StringSplitOptions.RemoveEmptyEntries).Take(10).ToArray();
            if (rows.Any(r=>
            {
                var p=r.Split('\t');
                return p.Length is not (3 or 4) || p[0] is not ("PvE" or "PvP")
                    || !(p[1] is "승" or "패" || p[0]=="PvP" && p[1] is "2위" or "3위" or "4위");
            })) return null;
            return new RecentHistory(true,rows);
        }
        catch (FormatException) { return null; }
    }
}
