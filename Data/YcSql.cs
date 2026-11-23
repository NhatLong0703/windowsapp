namespace WebApplication1.Data;

public static class YcSql
{
    // Một dòng yêu cầu nhập kèm tồn hiện tại của sản phẩm tại kho yêu cầu
    public const string Rows = @"
SELECT y.MaYC, y.MaKho, k.TenKho, y.MaSP, s.TenSP, s.DonViTinh, ISNULL(s.TonToiThieu, 0) AS TonToiThieu,
       y.MaNCC, n.TenNCC, y.SoLuong, ISNULL(t.Ton, 0) AS TonHienTai, y.GhiChu, y.TrangThai, y.LyDoTuChoi,
       y.NgayTao, nv.HoTen AS NguoiTao, y.NgayDuyet, y.NgayGiao, y.MaPNTao
FROM YeuCauNhap y
JOIN Kho k ON k.MaKho = y.MaKho
JOIN SanPham s ON s.MaSP = y.MaSP
JOIN NhaCungCap n ON n.MaNCC = y.MaNCC
JOIN NhanVien nv ON nv.MaNV = y.MaNVTao
LEFT JOIN (" + Sql.Stock + @") t ON t.MaKho = y.MaKho AND t.MaSP = y.MaSP";
}
