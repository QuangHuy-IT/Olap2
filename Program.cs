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
                        string lastPart = parts.Last();

                        // Phân loại cột: [M] cho Measure, [D] cho Dimension
                        string cleanName;
                        if (rawName.Contains("Measures")) {
                            cleanName = $"[M] {lastPart}";
                        } else {
                            string dimName = parts[0].Replace("Dim ", "");
                            cleanName = parts.Length >= 2 ? $"[D] {dimName} | {lastPart}" : $"[D] {lastPart}";
                        }
                        
                        row[cleanName] = dr[i]?.ToString() ?? "0";
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
        { "Nam", "[Dim Thoi Gian].[Nam]" },
        { "Quy", "[Dim Thoi Gian].[Quy]" },
        { "Thang", "[Dim Thoi Gian].[Thang]" },
        { "BangCH", "[Dim Cua Hang].[Bang]" },
        { "ThanhPhoCH", "[Dim Cua Hang].[Ten Thanh Pho]" },
        { "LoaiKH", "[Dim Khach Hang].[Loai Khach Hang]" },
        { "TenKH", "[Dim Khach Hang].[Ten KH]" },
        { "MoTa", "[Dim Mat Hang].[Mo Ta]" }
    };

    using (AdomdConnection conn = new AdomdConnection(connString))
    {
        conn.Open();
        foreach (var item in levels)
        {
            var members = new List<FilterItem>();
            try {
                string mdx = $@"WITH MEMBER [Measures].[UName] AS {item.Value}.CurrentMember.UniqueName 
                               SELECT {{ [Measures].[UName] }} ON 0, 
                               {item.Value}.Children ON 1 FROM [DW Online Retail]";
                using (AdomdCommand cmd = new AdomdCommand(mdx, conn))
                using (AdomdDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read()) {
                        string label = dr[0]?.ToString(); 
                        string id = dr[1]?.ToString();    
                        if (!string.IsNullOrEmpty(label)) members.Add(new FilterItem(id, label));
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

    var axisRows = req.Dimensions.Select(dim => dim switch {
        "Nam" => "[Dim Thoi Gian].[Nam].Children",
        "Quy" => "[Dim Thoi Gian].[Quy].Children",
        "Thang" => "[Dim Thoi Gian].[Thang].Children",
        "BangCH" => "[Dim Cua Hang].[Bang].Children",
        "ThanhPhoCH" => "[Dim Cua Hang].[Ten Thanh Pho].Children",
        "LoaiKH" => "[Dim Khach Hang].[Loai Khach Hang].Children",
        "TenKH" => "[Dim Khach Hang].[Ten KH].Children",
        "MoTa" => "[Dim Mat Hang].[Mo Ta].Children",
        _ => ""
    }).Where(s => !string.IsNullOrEmpty(s)).ToList();

    if (axisRows.Count == 0) axisRows.Add("[Dim Mat Hang].[Mo Ta].Children");

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