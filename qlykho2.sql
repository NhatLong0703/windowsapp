
-- ============================================
-- 1. TAO CO SO DU LIEU QUAN LY KHO HANG
-- ============================================

IF DB_ID(N'QuanLyKhoHang') IS NULL
BEGIN
    CREATE DATABASE QuanLyKhoHang;
END
GO

USE QuanLyKhoHang;
GO

-- ============================================
-- 2. BANG NHAN VIEN
-- ============================================

IF OBJECT_ID('NhanVien', 'U') IS NULL
BEGIN
    CREATE TABLE NhanVien (
        MaNV INT IDENTITY(1,1) PRIMARY KEY,
        HoTen NVARCHAR(100) NOT NULL,
        GioiTinh NVARCHAR(10),
        NgaySinh DATE,
        SoDienThoai VARCHAR(15),
        DiaChi NVARCHAR(200),
        ChucVu NVARCHAR(50)
    );
END
GO

-- ============================================
-- 3. BANG KHO
-- ============================================

IF OBJECT_ID('Kho', 'U') IS NULL
BEGIN
    CREATE TABLE Kho (
        MaKho INT IDENTITY(1,1) PRIMARY KEY,
        TenKho NVARCHAR(100) NOT NULL,
        DiaChi NVARCHAR(200),
        SoDienThoai VARCHAR(15)
    );
END
GO

-- ============================================
-- 4. BANG SAN PHAM
-- ============================================

IF OBJECT_ID('SanPham', 'U') IS NULL
BEGIN
    CREATE TABLE SanPham (
        MaSP INT IDENTITY(1,1) PRIMARY KEY,
        TenSP NVARCHAR(100) NOT NULL,
        DonViTinh NVARCHAR(30),
        DonGia DECIMAL(18,2),
        MoTa NVARCHAR(200)
    );
END
GO

-- ============================================
-- 5. BANG NHA CUNG CAP
-- ============================================

IF OBJECT_ID('NhaCungCap', 'U') IS NULL
BEGIN
    CREATE TABLE NhaCungCap (
        MaNCC INT IDENTITY(1,1) PRIMARY KEY,
        TenNCC NVARCHAR(100) NOT NULL,
        SoDienThoai VARCHAR(15),
        DiaChi NVARCHAR(200),
        Email VARCHAR(100)
    );
END
GO

-- ============================================
-- 6. BANG PHIEU NHAP
-- ============================================

IF OBJECT_ID('PhieuNhap', 'U') IS NULL
BEGIN
    CREATE TABLE PhieuNhap (
        MaPN INT IDENTITY(1,1) PRIMARY KEY,
        MaNV INT NOT NULL,
        MaKho INT NOT NULL,
        MaNCC INT NOT NULL,
        NgayNhap DATE NOT NULL,
        GhiChu NVARCHAR(200),

        FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV),
        FOREIGN KEY (MaKho) REFERENCES Kho(MaKho),
        FOREIGN KEY (MaNCC) REFERENCES NhaCungCap(MaNCC)
    );
END
GO

-- ============================================
-- 7. BANG CHI TIET PHIEU NHAP
-- ============================================

IF OBJECT_ID('ChiTietPhieuNhap', 'U') IS NULL
BEGIN
    CREATE TABLE ChiTietPhieuNhap (
        MaCTPN INT IDENTITY(1,1) PRIMARY KEY,
        MaPN INT NOT NULL,
        MaSP INT NOT NULL,
        SoLuong INT NOT NULL,
        DonGiaNhap DECIMAL(18,2) NOT NULL,

        FOREIGN KEY (MaPN) REFERENCES PhieuNhap(MaPN),
        FOREIGN KEY (MaSP) REFERENCES SanPham(MaSP),

        CHECK (SoLuong > 0),
        CHECK (DonGiaNhap >= 0)
    );
END
GO

-- ============================================
-- 8. BANG PHIEU XUAT
-- ============================================

IF OBJECT_ID('PhieuXuat', 'U') IS NULL
BEGIN
    CREATE TABLE PhieuXuat (
        MaPX INT IDENTITY(1,1) PRIMARY KEY,
        MaNV INT NOT NULL,
        MaKho INT NOT NULL,
        NgayXuat DATE NOT NULL,
        GhiChu NVARCHAR(200),

        FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV),
        FOREIGN KEY (MaKho) REFERENCES Kho(MaKho)
    );
END
GO

-- ============================================
-- 9. BANG CHI TIET PHIEU XUAT
-- ============================================

IF OBJECT_ID('ChiTietPhieuXuat', 'U') IS NULL
BEGIN
    CREATE TABLE ChiTietPhieuXuat (
        MaCTPX INT IDENTITY(1,1) PRIMARY KEY,
        MaPX INT NOT NULL,
        MaSP INT NOT NULL,
        SoLuong INT NOT NULL,
        DonGiaXuat DECIMAL(18,2) NOT NULL,

        FOREIGN KEY (MaPX) REFERENCES PhieuXuat(MaPX),
        FOREIGN KEY (MaSP) REFERENCES SanPham(MaSP),

        CHECK (SoLuong > 0),
        CHECK (DonGiaXuat >= 0)
    );
END
GO

-- ============================================
-- 10. KIEM TRA CAC BANG
-- ============================================

SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE';
GO

-- ============================================
-- 11. KIEM TRA DU LIEU CAC BANG
-- ============================================

SELECT * FROM NhanVien;
SELECT * FROM Kho;
SELECT * FROM SanPham;
SELECT * FROM NhaCungCap;
SELECT * FROM PhieuNhap;
SELECT * FROM ChiTietPhieuNhap;
SELECT * FROM PhieuXuat;
SELECT * FROM ChiTietPhieuXuat;
GO
