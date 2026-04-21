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

// HÀM DÒ TÌM ĐƯỜNG DẪN MÃ ĐƠN HÀNG THỰC TẾ
string GetOrderPath() {
    Console.WriteLine("\n--- ĐANG TRUY TÌM CHIỀU MÃ ĐƠN HÀNG TRONG CUBE ---");
    try {
        using (AdomdConnection conn = new AdomdConnection(connString)) {
            conn.Open();
            var cube = conn.Cubes["DW Online Retail"];
            foreach (Dimension dim in cube.Dimensions) {
                if (dim.Name.StartsWith("$")) continue;
                foreach (Hierarchy hier in dim.Hierarchies) {
                    string hName = hier.Name.ToLower();
                    string dName = dim.Name.ToLower();
                    // Tìm kiếm chính xác mã đơn, bỏ qua đơn giá
                    if ((hName.Contains("ma don") || hName.Contains("madon") || hName.Contains("don hang")) 
                        && !hName.Contains("gia")) {
                        string path = $"[{dim.Name}].[{hier.Name}]";
                        Console.WriteLine($"=> TÌM THẤY CHIỀU ĐƠN HÀNG: {path}");
                        return path;
                    }
                }
            }
        }
    } catch { }
    // Fallback nếu không thấy (bạn cần kiểm tra xem đã add MaDon vào Dimension của Cube chưa)
    return "[Dim Khach Hang].[Ten KH]"; 
}

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
    } catch (Exception ex) { Console.WriteLine("ERR: " + ex.Message); }
    return results;
}

app.MapGet("/api/filters", () => {
    var filters = new Dictionary<string, List<FilterItem>>();
    string orderPath = GetOrderPath();
    var levels = new Dictionary<string, string> {
        { "Nam", "[Dim Thoi Gian].[Nam]" }, { "Quy", "[Dim Thoi Gian].[Quy]" }, 
        { "Thang", "[Dim Thoi Gian].[Thang]" }, { "Ngay", "[Dim Thoi Gian].[Ngay]" },
        { "BangCH", "[Dim Cua Hang].[Bang]" }, { "ThanhPhoCH", "[Dim Cua Hang].[Ten Thanh Pho]" },
        { "TenCH", "[Dim Cua Hang].[Ten Cua Hang]" },
        { "LoaiKH", "[Dim Khach Hang].[Loai Khach Hang]" }, { "TenKH", "[Dim Khach Hang].[Ten KH]" },
        { "TenMH", "[Dim Mat Hang].[Ten Mat Hang]" },
        { "DonHang", orderPath } 
    };

    using (AdomdConnection conn = new AdomdConnection(connString))
    {
        conn.Open();
        foreach (var item in levels)
        {
            var members = new List<FilterItem>();
            try {
                string mdx = $@"WITH MEMBER [Measures].[UName] AS {item.Value}.CurrentMember.UniqueName 
                               MEMBER [Measures].[Cap] AS {item.Value}.CurrentMember.Member_Caption
                               SELECT {{ [Measures].[UName], [Measures].[Cap] }} ON 0, 
                               {item.Value}.Children ON 1 FROM [DW Online Retail]";
                using (AdomdCommand cmd = new AdomdCommand(mdx, conn))
                using (AdomdDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read()) {
                        string id = dr[1]?.ToString(); 
                        string label = dr[2]?.ToString();    
                        if (!string.IsNullOrEmpty(id) && !id.Contains("All")) members.Add(new FilterItem(id, label ?? id));
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

    string orderPath = GetOrderPath();
    var axisSet = new HashSet<string>();

    foreach (var dim in req.Dimensions) {
        string path = dim switch {
            "Nam" => "[Dim Thoi Gian].[Nam].Children",
            "Quy" => "[Dim Thoi Gian].[Quy].Children",
            "Thang" => "[Dim Thoi Gian].[Thang].Children",
            "Ngay" => "[Dim Thoi Gian].[Ngay].Children",
            "BangCH" => "[Dim Cua Hang].[Bang].Children",
            "ThanhPhoCH" => "[Dim Cua Hang].[Ten Thanh Pho].Children",
            "TenCH" => "[Dim Cua Hang].[Ten Cua Hang].Children",
            "LoaiKH" => "[Dim Khach Hang].[Loai Khach Hang].Children",
            "TenKH" => "[Dim Khach Hang].[Ten KH].Children",
            "TenMH" => "[Dim Mat Hang].[Ten Mat Hang].Children",
            "DonHang" => $"{orderPath}.Children",
            _ => ""
        };
        if (!string.IsNullOrEmpty(path)) axisSet.Add(path);
        
        if (dim == "TenCH") axisSet.Add("[Dim Cua Hang].[So Dien Thoai].Children");
        if (dim == "TenMH") {
            axisSet.Add("[Dim Mat Hang].[Kich Co].Children");
            axisSet.Add("[Dim Mat Hang].[Trong Luong].Children");
            axisSet.Add("[Dim Mat Hang].[Don Gia].Children");
        }
    }

    if (axisSet.Count == 0) axisSet.Add("[Dim Mat Hang].[Ten Mat Hang].Children");

    string fromClause = "[DW Online Retail]";
    if (req.Filters != null) {
        foreach (var f in req.Filters.Where(x => x.Value != "All")) {
            fromClause = $"(SELECT {{ {f.Value} }} ON 0 FROM {fromClause})";
        }
    }

    string mdx = $"SELECT NON EMPTY {measures} ON 0, NON EMPTY {{ {string.Join(" * ", axisSet)} }} ON 1 FROM {fromClause}";
    return Results.Ok(ExecuteOlapQuery(mdx));
});

app.MapGet("/", () => Results.File(Path.Combine(Directory.GetCurrentDirectory(), "index.html"), "text/html"));
app.Run();

public record FilterItem(string Id, string Label);
public record QueryRequest(string Cube, List<string> Dimensions, Dictionary<string, string>? Filters);