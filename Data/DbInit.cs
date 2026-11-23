using Dapper;
using WebApplication1.Helpers;

namespace WebApplication1.Data;

// Chạy mỗi lần mở app. Mọi lệnh đều "nếu chưa có thì mới tạo" nên chạy lại nhiều lần vẫn an toàn.
public static class DbInit
{
    public static void Run(Db db)
    {
        using var c = db.Open();

        if (c.ExecuteScalar<int>("SELECT CASE WHEN OBJECT_ID('NhanVien','U') IS NULL THEN 0 ELSE 1 END") == 0)
            throw new InvalidOperationException("Chưa có các bảng của CSDL QuanLyKhoHang. Hãy chạy file qlykho2.sql trong SSMS trước.");

        foreach (var sql in Batches) c.Execute(sql);

        if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM TaiKhoan") == 0)
        {
            var maNV = c.ExecuteScalar<int>(
                "INSERT NhanVien(HoTen, ChucVu) VALUES (N'Quản trị viên', N'Quản trị'); SELECT CAST(SCOPE_IDENTITY() AS INT);");
            c.Execute("INSERT TaiKhoan(TenDangNhap, MatKhauHash, VaiTro, MaNV) VALUES ('admin', @h, 'Admin', @n)",
                new { h = Pw.Hash("admin123"), n = maNV });
        }
    }

    private static readonly string[] Batches =
    {
@"IF OBJECT_ID('LoaiHang','U') IS NULL
  CREATE TABLE LoaiHang (
    MaLoai INT IDENTITY(1,1) PRIMARY KEY,
    TenLoai NVARCHAR(100) NOT NULL UNIQUE)",

@"IF COL_LENGTH('SanPham','MaLoai') IS NULL
  ALTER TABLE SanPham ADD MaLoai INT NULL CONSTRAINT FK_SanPham_LoaiHang REFERENCES LoaiHang(MaLoai)",

@"IF COL_LENGTH('SanPham','TonToiThieu') IS NULL
  ALTER TABLE SanPham ADD TonToiThieu INT NOT NULL CONSTRAINT DF_SanPham_TonToiThieu DEFAULT 10",

@"IF OBJECT_ID('NCC_LoaiHang','U') IS NULL
  CREATE TABLE NCC_LoaiHang (
    MaNCC INT NOT NULL REFERENCES NhaCungCap(MaNCC) ON DELETE CASCADE,
    MaLoai INT NOT NULL REFERENCES LoaiHang(MaLoai) ON DELETE CASCADE,
    PRIMARY KEY (MaNCC, MaLoai))",

@"IF OBJECT_ID('NguoiDaiDien','U') IS NULL
  CREATE TABLE NguoiDaiDien (
    MaDD INT IDENTITY(1,1) PRIMARY KEY,
    MaNCC INT NOT NULL REFERENCES NhaCungCap(MaNCC) ON DELETE CASCADE,
    HoTen NVARCHAR(100) NOT NULL,
    ChucVu NVARCHAR(50),
    SoDienThoai VARCHAR(15),
    Email VARCHAR(100))",

// ---- Nhà bán lẻ ----
@"IF OBJECT_ID('NhaBanLe','U') IS NULL
  CREATE TABLE NhaBanLe (
    MaBL INT IDENTITY(1,1) PRIMARY KEY,
    TenBL NVARCHAR(100) NOT NULL,
    NguoiLienHe NVARCHAR(100),
    SoDienThoai VARCHAR(15),
    DiaChi NVARCHAR(200),
    Email VARCHAR(100))",

// ---- Tài khoản: Admin / ThuKho / BanLe ----
@"IF OBJECT_ID('TaiKhoan','U') IS NULL
  CREATE TABLE TaiKhoan (
    MaTK INT IDENTITY(1,1) PRIMARY KEY,
    TenDangNhap VARCHAR(50) NOT NULL UNIQUE,
    MatKhauHash VARCHAR(300) NOT NULL,
    VaiTro VARCHAR(20) NOT NULL,
    MaNV INT NULL REFERENCES NhanVien(MaNV),
    MaBL INT NULL REFERENCES NhaBanLe(MaBL),
    HoatDong BIT NOT NULL DEFAULT 1)",

@"IF COL_LENGTH('TaiKhoan','MaBL') IS NULL
  ALTER TABLE TaiKhoan ADD MaBL INT NULL CONSTRAINT FK_TaiKhoan_NhaBanLe REFERENCES NhaBanLe(MaBL)",

@"IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaiKhoan') AND name = 'MaNV' AND is_nullable = 0)
  ALTER TABLE TaiKhoan ALTER COLUMN MaNV INT NULL",

@"DECLARE @n sysname = (SELECT TOP 1 name FROM sys.check_constraints
   WHERE parent_object_id = OBJECT_ID('TaiKhoan') AND definition LIKE '%VaiTro%' AND definition NOT LIKE '%BanLe%');
  IF @n IS NOT NULL EXEC('ALTER TABLE TaiKhoan DROP CONSTRAINT [' + @n + ']')",

@"UPDATE TaiKhoan SET VaiTro = 'ThuKho' WHERE VaiTro = 'NhanVien'",

@"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TaiKhoan_VaiTro')
  ALTER TABLE TaiKhoan ADD CONSTRAINT CK_TaiKhoan_VaiTro CHECK (VaiTro IN ('Admin','ThuKho','BanLe'))",

@"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TaiKhoan_LienKet')
  ALTER TABLE TaiKhoan ADD CONSTRAINT CK_TaiKhoan_LienKet CHECK (
    (VaiTro = 'BanLe' AND MaBL IS NOT NULL AND MaNV IS NULL) OR (VaiTro <> 'BanLe' AND MaNV IS NOT NULL AND MaBL IS NULL))",

// ---- Thủ kho gắn với đúng 1 kho (do quản lý phân công) ----
@"IF COL_LENGTH('NhanVien','MaKho') IS NULL
  ALTER TABLE NhanVien ADD MaKho INT NULL CONSTRAINT FK_NhanVien_Kho REFERENCES Kho(MaKho)",

// ---- Đơn đặt mua của nhà bán lẻ ----
@"IF OBJECT_ID('DonDatMua','U') IS NULL
  CREATE TABLE DonDatMua (
    MaDon INT IDENTITY(1,1) PRIMARY KEY,
    MaBL INT NOT NULL REFERENCES NhaBanLe(MaBL),
    MaKho INT NOT NULL REFERENCES Kho(MaKho),
    NgayDat DATETIME NOT NULL DEFAULT GETDATE(),
    TrangThai VARCHAR(20) NOT NULL DEFAULT 'ChoXacNhan'
        CHECK (TrangThai IN ('ChoXacNhan','DaXacNhan','TuChoi','DaHuy')),
    GhiChu NVARCHAR(200) NULL,
    LyDoTuChoi NVARCHAR(200) NULL,
    MaNVXuLy INT NULL REFERENCES NhanVien(MaNV),
    NgayXuLy DATETIME NULL,
    MaPX INT NULL REFERENCES PhieuXuat(MaPX))",

@"IF OBJECT_ID('ChiTietDonDatMua','U') IS NULL
  CREATE TABLE ChiTietDonDatMua (
    MaCT INT IDENTITY(1,1) PRIMARY KEY,
    MaDon INT NOT NULL REFERENCES DonDatMua(MaDon) ON DELETE CASCADE,
    MaSP INT NOT NULL REFERENCES SanPham(MaSP),
    SoLuong INT NOT NULL CHECK (SoLuong > 0),
    DonGia DECIMAL(18,2) NOT NULL)"
    };
}
