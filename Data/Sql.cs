namespace WebApplication1.Data;

public static class Sql
{
    // Tồn kho = tổng nhập - tổng xuất theo (kho, sản phẩm)
    public const string Stock = @"
SELECT k.MaKho, k.TenKho, s.MaSP, s.TenSP, s.DonViTinh, ISNULL(s.DonGia,0) AS DonGia, s.TonToiThieu,
       ISNULL(n.SL,0) - ISNULL(x.SL,0) AS Ton
FROM Kho k CROSS JOIN SanPham s
LEFT JOIN (SELECT p.MaKho, c.MaSP, SUM(c.SoLuong) SL FROM PhieuNhap p JOIN ChiTietPhieuNhap c ON c.MaPN = p.MaPN GROUP BY p.MaKho, c.MaSP) n
       ON n.MaKho = k.MaKho AND n.MaSP = s.MaSP
LEFT JOIN (SELECT p.MaKho, c.MaSP, SUM(c.SoLuong) SL FROM PhieuXuat p JOIN ChiTietPhieuXuat c ON c.MaPX = p.MaPX GROUP BY p.MaKho, c.MaSP) x
       ON x.MaKho = k.MaKho AND x.MaSP = s.MaSP
WHERE n.SL IS NOT NULL OR x.SL IS NOT NULL";

    public const string PhieuNhapRows = @"
SELECT 'nhap' AS Loai, p.MaPN AS Id,
       'PN-' + CAST(YEAR(p.NgayNhap) AS VARCHAR(4)) + '-' + RIGHT('0000' + CAST(p.MaPN AS VARCHAR(10)), 4) AS Ma,
       p.NgayNhap AS Ngay, p.MaKho, k.TenKho, ncc.TenNCC AS DoiTac, nv.HoTen AS NguoiTao, p.GhiChu,
       ISNULL((SELECT SUM(c.SoLuong * c.DonGiaNhap) FROM ChiTietPhieuNhap c WHERE c.MaPN = p.MaPN), 0) AS GiaTri
FROM PhieuNhap p
JOIN Kho k ON k.MaKho = p.MaKho
JOIN NhaCungCap ncc ON ncc.MaNCC = p.MaNCC
JOIN NhanVien nv ON nv.MaNV = p.MaNV";

    public const string PhieuXuatRows = @"
SELECT 'xuat' AS Loai, p.MaPX AS Id,
       'PX-' + CAST(YEAR(p.NgayXuat) AS VARCHAR(4)) + '-' + RIGHT('0000' + CAST(p.MaPX AS VARCHAR(10)), 4) AS Ma,
       p.NgayXuat AS Ngay, p.MaKho, k.TenKho, CAST(NULL AS NVARCHAR(100)) AS DoiTac, nv.HoTen AS NguoiTao, p.GhiChu,
       ISNULL((SELECT SUM(c.SoLuong * c.DonGiaXuat) FROM ChiTietPhieuXuat c WHERE c.MaPX = p.MaPX), 0) AS GiaTri
FROM PhieuXuat p
JOIN Kho k ON k.MaKho = p.MaKho
JOIN NhanVien nv ON nv.MaNV = p.MaNV";

    public const string DonRows = @"
SELECT d.MaDon AS Id,
       'DM-' + CAST(YEAR(d.NgayDat) AS VARCHAR(4)) + '-' + RIGHT('0000' + CAST(d.MaDon AS VARCHAR(10)), 4) AS Ma,
       d.NgayDat, d.MaBL, b.TenBL, d.MaKho, k.TenKho, d.TrangThai, d.GhiChu, d.LyDoTuChoi,
       nv.HoTen AS NguoiXuLy, d.NgayXuLy, d.MaPX,
       (SELECT COUNT(*) FROM ChiTietDonDatMua c WHERE c.MaDon = d.MaDon) AS SoDong,
       ISNULL((SELECT SUM(c.SoLuong * c.DonGia) FROM ChiTietDonDatMua c WHERE c.MaDon = d.MaDon), 0) AS GiaTri
FROM DonDatMua d
JOIN NhaBanLe b ON b.MaBL = d.MaBL
JOIN Kho k ON k.MaKho = d.MaKho
LEFT JOIN NhanVien nv ON nv.MaNV = d.MaNVXuLy";
}
