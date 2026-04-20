USE [DW_OnlineRetail]
GO
/****** Object:  Table [dbo].[Dim_CuaHang]    Script Date: 4/18/2026 6:02:09 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Dim_CuaHang](
	[MaCuaHang] [varchar](50) NOT NULL,
	[TenCuaHang] [nvarchar](150) NULL,
	[TenThanhPho] [nvarchar](100) NULL,
	[Bang] [nvarchar](100) NULL,
	[SoDienThoai] [varchar](20) NULL,
PRIMARY KEY CLUSTERED 
(
	[MaCuaHang] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Dim_KhachHang]    Script Date: 4/18/2026 6:02:09 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Dim_KhachHang](
	[MaKH] [varchar](50) NOT NULL,
	[TenKH] [nvarchar](150) NULL,
	[TenThanhPho] [nvarchar](100) NULL,
	[Bang] [nvarchar](100) NULL,
	[LoaiKhachHang] [nvarchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[MaKH] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Dim_MatHang]    Script Date: 4/18/2026 6:02:09 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Dim_MatHang](
	[MaMH] [varchar](50) NOT NULL,
	[TenMatHang] [nvarchar](255) NULL,
	[MoTa] [nvarchar](max) NULL,
	[KichCo] [varchar](50) NULL,
	[TrongLuong] [int] NULL,
	[DonGia] [bigint] NULL,
PRIMARY KEY CLUSTERED 
(
	[MaMH] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Dim_ThoiGian]    Script Date: 4/18/2026 6:02:09 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Dim_ThoiGian](
	[MaThoiGian] [int] NOT NULL,
	[Ngay] [int] NULL,
	[Thang] [int] NULL,
	[Quy] [int] NULL,
	[Nam] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[MaThoiGian] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Fact_BanHang]    Script Date: 4/18/2026 6:02:09 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Fact_BanHang](
	[MaDon] [varchar](50) NULL,
	[MaKH] [varchar](50) NULL,
	[MaMH] [varchar](50) NULL,
	[MaThoiGian] [int] NULL,
	[MaCuaHang] [varchar](50) NULL,
	[SoLuongDat] [int] NULL,
	[ThanhTien] [bigint] NULL
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Fact_TonKho]    Script Date: 4/18/2026 6:02:09 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Fact_TonKho](
	[MaCuaHang] [varchar](50) NULL,
	[MaMH] [varchar](50) NULL,
	[MaThoiGian] [int] NULL,
	[SoLuongTrongKho] [int] NULL
)