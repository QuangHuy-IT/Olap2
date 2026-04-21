using Microsoft.AnalysisServices.AdomdClient;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

string connString = "Data Source=DESKTOP-C4RM5V7\\OLAP;Catalog=SSAS_OnlineRetail;";

List<Dictionary<string, object>> ExecuteOlapQuery(string mdxQuery)
{
    Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] MDX:\n{mdxQuery}");
    var results = new List<Dictionary<string, object>>();
    try {
        using (AdomdConnection conn = new AdomdConnection(connString))
        {
            conn.Open();
            using (AdomdCommand cmd = new AdomdCommand(mdxQuery, conn))
            using (AdomdDataReader dr = cmd.ExecuteReader())
            {
                while (dr.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < dr.FieldCount; i++)
                    {
                        string rawName = dr.GetName(i);
                        string[] parts = rawName.Replace("[", "").Replace("]", "").Split('.');
                        string lastPart = parts.Last().Replace("MEMBER_CAPTION", "").Replace("Member_Caption", "").Trim();
                        if (string.IsNullOrEmpty(lastPart) && parts.Length >= 2) lastPart = parts[parts.Length - 2];

                        string cleanName;
                        if (rawName.Contains("Measures")) cleanName = $"[M] {lastPart}";
                        else {
                            string dimName = parts[0].Replace("Dim ", "");
                            cleanName = parts.Length >= 2 ? $"[D] {dimName} | {lastPart}" : $"[D] {lastPart}";
                        }
                        row[cleanName.TrimEnd(' ', '|')] = dr[i]?.ToString() ?? "0";
                    }
                    results.Add(row);
                }
            }
        }
    } catch (Exception ex) { Console.WriteLine("ERR MDX: " + ex.Message); }
    return results;
}

app.MapGet("/api/filters", () => {
    var filters = new Dictionary<string, List<FilterItem>>();
    var levels = new Dictionary<string, string> {
        { "Nam", "[Dim Thoi Gian].[Nam]" }, { "Quy", "[Dim Thoi Gian].[Quy]" }, 
        { "Thang", "[Dim Thoi Gian].[Thang]" }, { "Ngay", "[Dim Thoi Gian].[Ngay]" },
        { "BangCH", "[Dim Cua Hang].[Bang]" }, { "ThanhPhoCH", "[Dim Cua Hang].[Ten Thanh Pho]" },
        { "TenCH", "[Dim Cua Hang].[Ten Cua Hang]" },
        { "LoaiKH", "[Dim Khach Hang].[Loai Khach Hang]" }, { "TenKH", "[Dim Khach Hang].[Ten KH]" },
        { "TenMH", "[Dim Mat Hang].[Ten Mat Hang]" }
    };

    using (AdomdConnection conn = new AdomdConnection(connString))
    {
        conn.Open();
        foreach (var item in levels)
        {
            var members = new List<FilterItem>();
            try {
                string mdx = $@"WITH MEMBER [Measures].[UName] AS {item.Value}.CurrentMember.UniqueName 
                               SELECT {{ [Measures].[UName] }} ON 0, {item.Value}.Children ON 1 FROM [DW Online Retail]";
                using (AdomdCommand cmd = new AdomdCommand(mdx, conn))
                using (AdomdDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read()) {
                        if (!string.IsNullOrEmpty(dr[0]?.ToString()))
                            members.Add(new FilterItem(dr[1]?.ToString(), dr[0]?.ToString()));
                    }
                }
            } catch { }
            filters[item.Key] = members.OrderBy(x => x.Label).ToList();
        }
    }
    return Results.Ok(filters);
});

app.MapPost("/api/query", (QueryRequest req) => {
    string measures = req.Cube == "BanHang" 
        ? "{ [Measures].[So Luong Dat], [Measures].[Thanh Tien], [Measures].[Fact Ban Hang Count] }" 
        : "{ [Measures].[So Luong Trong Kho] }";

    var axisRows = new List<string>();
    foreach (var dim in req.Dimensions) {
        switch (dim) {
            case "Nam": axisRows.Add("[Dim Thoi Gian].[Nam].Children"); break;
            case "Quy": axisRows.Add("[Dim Thoi Gian].[Quy].Children"); break;
            case "Thang": axisRows.Add("[Dim Thoi Gian].[Thang].Children"); break;
            case "Ngay": axisRows.Add("[Dim Thoi Gian].[Ngay].Children"); break;
            case "BangCH": axisRows.Add("[Dim Cua Hang].[Bang].Children"); break;
            case "ThanhPhoCH": axisRows.Add("[Dim Cua Hang].[Ten Thanh Pho].Children"); break;
            case "TenCH": 
                axisRows.Add("[Dim Cua Hang].[Ten Cua Hang].Children");
                axisRows.Add("[Dim Cua Hang].[So Dien Thoai].Children");
                break;
            case "LoaiKH": axisRows.Add("[Dim Khach Hang].[Loai Khach Hang].Children"); break;
            case "TenKH": axisRows.Add("[Dim Khach Hang].[Ten KH].Children"); break;
            case "TenMH": 
                axisRows.Add("[Dim Mat Hang].[Ten Mat Hang].Children");
                axisRows.Add("[Dim Mat Hang].[Kich Co].Children");
                axisRows.Add("[Dim Mat Hang].[Trong Luong].Children");
                axisRows.Add("[Dim Mat Hang].[Don Gia].Children");
                break;
        }
    }

    if (axisRows.Count == 0) axisRows.Add("[Dim Mat Hang].[Ten Mat Hang].Children");

    string fromClause = "[DW Online Retail]";
    if (req.Filters != null) {
        foreach (var f in req.Filters.Where(x => x.Value != "All")) {
            fromClause = $"(SELECT {{ {f.Value} }} ON 0 FROM {fromClause})";
        }
    }

    string mdx = $"SELECT NON EMPTY {measures} ON 0, NON EMPTY {{ {string.Join(" * ", axisRows)} }} ON 1 FROM {fromClause}";
    return Results.Ok(ExecuteOlapQuery(mdx));
});

app.MapGet("/", () => Results.File(Path.Combine(Directory.GetCurrentDirectory(), "index.html"), "text/html"));
app.Run();

public record FilterItem(string Id, string Label);
public record QueryRequest(string Cube, List<string> Dimensions, Dictionary<string, string>? Filters);