using Microsoft.AnalysisServices.AdomdClient;
using System.Collections.Generic;
using System.Linq;
using System;

var builder = WebApplication.CreateBuilder(args);

// Mở CORS để Lát Frontend gọi API không bị lỗi
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();

// Hàm xử lý chung để thực thi MDX và trả về danh sách Dictionary động
List<Dictionary<string, object>> ExecuteOlapQuery(string mdxQuery)
{
    string connString = "Data Source=DESKTOP-C4RM5V7\\OLAP;Catalog=SSAS_OnlineRetail;";
    var results = new List<Dictionary<string, object>>();

    using (AdomdConnection conn = new AdomdConnection(connString))
    {
        conn.Open();
        using (AdomdCommand cmd = new AdomdCommand(mdxQuery, conn))
        {
            using (AdomdDataReader dr = cmd.ExecuteReader())
            {
                while (dr.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < dr.FieldCount; i++)
                    {
                        // Lấy tên cột (ví dụ: "[Dim Cua Hang].[Ten Thanh Pho].[Ten Thanh Pho]")
                        string rawName = dr.GetName(i);
                        
                        // Làm sạch tên cột và ghép tên Dimension để tránh trùng lặp (ví dụ Cửa hàng và Khách hàng đều có Thành phố)
                        string[] parts = rawName.Split('.');
                        string cleanName = "";
                        if (rawName.Contains("Measures")) {
                            cleanName = parts.Last().Replace("[", "").Replace("]", "");
                        }
                        else if (parts.Length >= 2) {
                            string dimName = parts[0].Replace("[", "").Replace("]", "").Replace("Dim ", "");
                            string levelName = parts[1].Replace("[", "").Replace("]", "");
                            cleanName = dimName + " - " + levelName;
                        }
                        else {
                            cleanName = rawName.Replace("[", "").Replace("]", "");
                        }
                        
                        // Xử lý giá trị
                        object value = dr[i];
                        if (value == DBNull.Value || value == null)
                        {
                            row[cleanName] = "N/A";
                        }
                        else
                        {
                            row[cleanName] = value;
                        }
                    }
                    results.Add(row);
                }
            }
        }
    }
    return results;
}

// API Schema
app.MapGet("/api/schema", () => {
    string connString = "Data Source=DESKTOP-C4RM5V7\\OLAP;Catalog=SSAS_OnlineRetail;";
    var schema = new List<object>();
    try {
        using (AdomdConnection conn = new AdomdConnection(connString)) {
            conn.Open();
            foreach (CubeDef cube in conn.Cubes) {
                if (cube.Name.StartsWith("$")) continue;
                var cubeInfo = new {
                    CubeName = cube.Name,
                    Dimensions = new List<object>()
                };
                foreach (Dimension dim in cube.Dimensions) {
                    var dimInfo = new {
                        DimName = dim.Name,
                        Hierarchies = new List<object>()
                    };
                    foreach (Hierarchy hier in dim.Hierarchies) {
                        var hierInfo = new {
                            HierName = hier.Name,
                            Levels = new List<string>()
                        };
                        foreach (Level lvl in hier.Levels) {
                            hierInfo.Levels.Add(lvl.Name);
                        }
                        dimInfo.Hierarchies.Add(hierInfo);
                    }
                    cubeInfo.Dimensions.Add(dimInfo);
                }
                schema.Add(cubeInfo);
            }
        }
        return Results.Ok(schema);
    } catch(Exception ex) {
        return Results.Problem(ex.Message);
    }
});

// API Bán Hàng
app.MapGet("/api/sales", () => {
    // Thử query theo nhiều bộ measure để tương thích dữ liệu cube hiện tại.
    var salesQueries = new[] {
        @"
        SELECT 
            NON EMPTY { [Measures].[So Luong Dat], [Measures].[Thanh Tien] } ON COLUMNS,
            NON EMPTY { 
                [Dim Thoi Gian].[Ngay].Children *
                [Dim Khach Hang].[Ten KH].Children *
                [Dim Khach Hang].[Loai Khach Hang].Children *
                [Dim Khach Hang].[Ten Thanh Pho].Children *
                [Dim Khach Hang].[Bang].Children *
                [Dim Cua Hang].[Ma Cua Hang].Children *
                [Dim Cua Hang].[Ten Thanh Pho].Children *
                [Dim Cua Hang].[So Dien Thoai].Children *
                [Dim Mat Hang].[Mo Ta].Children
            } ON ROWS
        FROM [DW Online Retail]",
        @"
        SELECT 
            NON EMPTY { [Measures].[Sales Amount] } ON COLUMNS,
            NON EMPTY { 
                [Dim Thoi Gian].[Ngay].Children *
                [Dim Khach Hang].[Ten KH].Children *
                [Dim Khach Hang].[Loai Khach Hang].Children *
                [Dim Khach Hang].[Ten Thanh Pho].Children *
                [Dim Khach Hang].[Bang].Children *
                [Dim Cua Hang].[Ma Cua Hang].Children *
                [Dim Cua Hang].[Ten Thanh Pho].Children *
                [Dim Cua Hang].[So Dien Thoai].Children *
                [Dim Mat Hang].[Mo Ta].Children
            } ON ROWS
        FROM [DW Online Retail]"
    };

    Exception? lastError = null;
    foreach (var query in salesQueries)
    {
        try {
            var results = ExecuteOlapQuery(query);
            return Results.Ok(results);
        }
        catch (Exception ex) {
            lastError = ex;
        }
    }

    return Results.Problem("Lỗi MDX Bán Hàng: " + (lastError?.Message ?? "Không xác định"));
});

// API Tồn Kho
app.MapGet("/api/inventory", () => {
    string mdxQuery = @"
        SELECT 
            NON EMPTY { [Measures].[So Luong Trong Kho] } ON COLUMNS,
            NON EMPTY { 
                [Dim Cua Hang].[Ma Cua Hang].Children * 
                [Dim Cua Hang].[Ten Thanh Pho].Children * 
                [Dim Cua Hang].[Bang].Children * 
                [Dim Cua Hang].[So Dien Thoai].Children * 
                [Dim Mat Hang].[Mo Ta].Children *
                [Dim Mat Hang].[Kich Co].Children *
                [Dim Mat Hang].[Trong Luong].Children *
                [Dim Mat Hang].[Don Gia].Children
            } ON ROWS
        FROM [DW Online Retail]";

    try {
        var results = ExecuteOlapQuery(mdxQuery);
        return Results.Ok(results);
    }
    catch (Exception ex) {
        return Results.Problem("Lỗi MDX Tồn Kho: " + ex.Message);
    }
});

app.Run();