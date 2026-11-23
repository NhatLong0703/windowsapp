-- Chạy file này SAU khi đã chạy qlykho2.sql. Chạy lại nhiều lần cũng không sao.
USE QuanLyKhoHang;
GO

-- 1. Cột GhiChu cho phiếu xuất (sửa lỗi Invalid column name 'GhiChu')
IF COL_LENGTH('PhieuXuat', 'GhiChu') IS NULL
    ALTER TABLE PhieuXuat ADD GhiChu NVARCHAR(500) NULL;
GO

-- 2. Mỗi sản phẩm thuộc một nhà cung cấp
IF COL_LENGTH('SanPham', 'MaNCC') IS NULL
    ALTER TABLE SanPham ADD MaNCC INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SanPham_NhaCungCap')
    ALTER TABLE SanPham ADD CONSTRAINT FK_SanPham_NhaCungCap
        FOREIGN KEY (MaNCC) REFERENCES NhaCungCap(MaNCC);
GO

-- 3. Tài khoản đăng nhập cho nhà cung cấp
IF COL_LENGTH('TaiKhoan', 'MaNCC') IS NULL
    ALTER TABLE TaiKhoan ADD MaNCC INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TaiKhoan_NhaCungCap')
    ALTER TABLE TaiKhoan ADD CONSTRAINT FK_TaiKhoan_NhaCungCap
        FOREIGN KEY (MaNCC) REFERENCES NhaCungCap(MaNCC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TaiKhoan_MaNCC')
    CREATE UNIQUE INDEX UX_TaiKhoan_MaNCC ON TaiKhoan(MaNCC) WHERE MaNCC IS NOT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TaiKhoan_VaiTro')
    ALTER TABLE TaiKhoan DROP CONSTRAINT CK_TaiKhoan_VaiTro;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TaiKhoan_LienKet')
    ALTER TABLE TaiKhoan DROP CONSTRAINT CK_TaiKhoan_LienKet;
GO

ALTER TABLE TaiKhoan ADD CONSTRAINT CK_TaiKhoan_VaiTro
    CHECK ([VaiTro] IN ('BanLe', 'ThuKho', 'Admin', 'NhaCungCap'));

ALTER TABLE TaiKhoan ADD CONSTRAINT CK_TaiKhoan_LienKet CHECK (
       ([VaiTro] = 'BanLe'      AND [MaBL]  IS NOT NULL AND [MaNV] IS NULL AND [MaNCC] IS NULL)
    OR ([VaiTro] = 'NhaCungCap' AND [MaNCC] IS NOT NULL AND [MaNV] IS NULL AND [MaBL]  IS NULL)
    OR ([VaiTro] NOT IN ('BanLe', 'NhaCungCap') AND [MaNV] IS NOT NULL AND [MaBL] IS NULL AND [MaNCC] IS NULL)
);
GO

-- 4. Đề xuất sản phẩm mới của nhà cung cấp (gửi admin duyệt; duyệt xong có thể nhập thẳng vào kho)
--    TrangThai: ChoDuyet / DaDuyet / TuChoi / DaHuy
IF OBJECT_ID('DeXuatSanPham', 'U') IS NULL
CREATE TABLE DeXuatSanPham (
    MaDX       INT IDENTITY(1,1) PRIMARY KEY,
    MaNCC      INT NOT NULL REFERENCES NhaCungCap(MaNCC) ON DELETE CASCADE,
    TenSP      NVARCHAR(100) NOT NULL,
    DonViTinh  NVARCHAR(30)  NULL,
    DonGia     DECIMAL(18,2) NOT NULL DEFAULT 0 CHECK (DonGia >= 0),
    MoTa       NVARCHAR(200) NULL,
    MaLoai     INT NULL REFERENCES LoaiHang(MaLoai) ON DELETE SET NULL,
    SoLuong    INT NOT NULL DEFAULT 0 CHECK (SoLuong >= 0),   -- số lượng nhà cung cấp muốn giao
    MaKho      INT NULL REFERENCES Kho(MaKho) ON DELETE SET NULL,   -- kho nhận hàng
    TrangThai  NVARCHAR(20) NOT NULL DEFAULT N'ChoDuyet',
    LyDoTuChoi NVARCHAR(200) NULL,
    NgayTao    DATETIME NOT NULL DEFAULT GETDATE(),
    NgayDuyet  DATETIME NULL,
    MaSPTao    INT NULL,   -- sản phẩm được tạo khi duyệt
    MaPNTao    INT NULL,   -- phiếu nhập được tạo khi duyệt (nếu có nhập kho)
    CONSTRAINT CK_DeXuatSanPham_TT CHECK (TrangThai IN (N'ChoDuyet', N'DaDuyet', N'TuChoi', N'DaHuy'))
);
GO
