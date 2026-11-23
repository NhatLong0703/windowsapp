-- Chạy SAU cap_nhat_database.sql. Chạy lại nhiều lần cũng không sao.
-- Yêu cầu nhập hàng: Thủ kho tạo -> Admin duyệt -> Nhà cung cấp xác nhận giao -> tự tạo phiếu nhập vào kho.
-- TrangThai: ChoAdmin / ChoNCC / DaGiao / TuChoi (admin từ chối) / NCCTuChoi / DaHuy
USE QuanLyKhoHang;
GO
IF OBJECT_ID('YeuCauNhap', 'U') IS NULL
CREATE TABLE YeuCauNhap (
    MaYC        INT IDENTITY(1,1) PRIMARY KEY,
    MaKho       INT NOT NULL REFERENCES Kho(MaKho),
    MaSP        INT NOT NULL REFERENCES SanPham(MaSP),
    MaNCC       INT NOT NULL REFERENCES NhaCungCap(MaNCC),
    SoLuong     INT NOT NULL CHECK (SoLuong > 0),
    GhiChu      NVARCHAR(200) NULL,
    MaNVTao     INT NOT NULL REFERENCES NhanVien(MaNV),
    TrangThai   NVARCHAR(20) NOT NULL DEFAULT N'ChoAdmin',
    LyDoTuChoi  NVARCHAR(200) NULL,
    NgayTao     DATETIME NOT NULL DEFAULT GETDATE(),
    MaNVDuyet   INT NULL REFERENCES NhanVien(MaNV),
    NgayDuyet   DATETIME NULL,
    NgayGiao    DATETIME NULL,
    MaPNTao     INT NULL,
    CONSTRAINT CK_YeuCauNhap_TT CHECK (TrangThai IN (N'ChoAdmin', N'ChoNCC', N'DaGiao', N'TuChoi', N'NCCTuChoi', N'DaHuy'))
);
GO
