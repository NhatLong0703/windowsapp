using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

// Quản lý duyệt đề xuất sản phẩm mới của nhà cung cấp.
// Duyệt: tạo sản phẩm (gán cho nhà cung cấp đó) và, nếu có số lượng + kho, tạo luôn phiếu nhập để hàng vào thẳng kho.
[Authorize(Roles = "Admin")]
public class DeXuatController(Db db) : StaffController(db)
{
    public async Task<IActionResult> Index()
    {
        await using var c = db.Open();
        var rows = (await c.QueryAsync<DeXuatRow>(
            @"SELECT d.MaDX, d.MaNCC, n.TenNCC, d.TenSP, d.DonViTinh, d.DonGia, d.MoTa, d.MaLoai, l.TenLoai,
                     d.SoLuong, d.MaKho, k.TenKho, d.TrangThai, d.LyDoTuChoi, d.NgayTao, d.NgayDuyet, d.MaSPTao, d.MaPNTao
              FROM DeXuatSanPham d
              JOIN NhaCungCap n ON n.MaNCC = d.MaNCC
              LEFT JOIN Kho k ON k.MaKho = d.MaKho
              LEFT JOIN LoaiHang l ON l.MaLoai = d.MaLoai
              ORDER BY CASE d.TrangThai WHEN N'ChoDuyet' THEN 0 ELSE 1 END, d.NgayTao DESC, d.MaDX DESC")).ToList();
        ViewBag.Khos = (await c.QueryAsync<Kho>("SELECT MaKho, TenKho FROM Kho ORDER BY TenKho")).ToList();
        return View(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Duyet(int maDX, decimal? donGia, int? soLuong, int? maKho)
    {
        var gia = donGia ?? 0;
        var sl = soLuong ?? 0;
        int? kho = maKho is > 0 ? maKho : null;
        if (gia < 0 || sl < 0) { TempData["Err"] = "Đơn giá và số lượng không được âm."; return RedirectToAction(nameof(Index)); }
        if (sl > 0 && kho == null) { TempData["Err"] = "Muốn nhập hàng thì hãy chọn kho nhận."; return RedirectToAction(nameof(Index)); }

        try
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();

            var dx = await c.QueryFirstOrDefaultAsync<DeXuatRow>(
                "SELECT MaDX, MaNCC, TenSP, DonViTinh, MoTa, MaLoai, TrangThai FROM DeXuatSanPham WHERE MaDX = @maDX", new { maDX }, tx);
            if (dx == null || dx.TrangThai != "ChoDuyet")
            {
                tx.Rollback();
                TempData["Err"] = "Đề xuất không còn ở trạng thái chờ duyệt.";
                return RedirectToAction(nameof(Index));
            }
            if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SanPham WHERE TenSP = @TenSP", new { dx.TenSP }, tx) > 0)
            {
                tx.Rollback();
                TempData["Err"] = "Đã có sản phẩm trùng tên trong danh mục. Hãy từ chối đề xuất hoặc đổi tên sản phẩm đang có.";
                return RedirectToAction(nameof(Index));
            }

            // 1. Tạo sản phẩm, gán cho nhà cung cấp đề xuất
            var maSP = await c.ExecuteScalarAsync<int>(
                @"INSERT SanPham(TenSP, DonViTinh, DonGia, MoTa, MaLoai, TonToiThieu, MaNCC)
                  VALUES (@TenSP, @DonViTinh, @gia, @MoTa, @MaLoai, 10, @MaNCC);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { dx.TenSP, dx.DonViTinh, gia, dx.MoTa, dx.MaLoai, dx.MaNCC }, tx);

            // 2. Có số lượng và kho: tạo phiếu nhập để hàng vào thẳng kho
            int? maPN = null;
            if (sl > 0 && kho != null)
            {
                maPN = await c.ExecuteScalarAsync<int>(
                    @"INSERT PhieuNhap(MaNV, MaKho, MaNCC, NgayNhap, GhiChu)
                      VALUES (@MaNV, @kho, @MaNCC, @Ngay, @GhiChu);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { MaNV = User.MaNV(), kho, dx.MaNCC, Ngay = DateTime.Today, GhiChu = "Từ đề xuất sản phẩm #" + maDX }, tx);
                await c.ExecuteAsync(
                    "INSERT ChiTietPhieuNhap(MaPN, MaSP, SoLuong, DonGiaNhap) VALUES (@maPN, @maSP, @sl, @gia)",
                    new { maPN, maSP, sl, gia }, tx);
            }

            var n = await c.ExecuteAsync(
                @"UPDATE DeXuatSanPham
                  SET TrangThai = N'DaDuyet', NgayDuyet = GETDATE(), MaSPTao = @maSP, MaPNTao = @maPN,
                      DonGia = @gia, SoLuong = @sl, MaKho = @kho
                  WHERE MaDX = @maDX AND TrangThai = N'ChoDuyet'",
                new { maSP, maPN, gia, sl, kho, maDX }, tx);
            if (n == 0) { tx.Rollback(); TempData["Err"] = "Đề xuất vừa được xử lý ở nơi khác."; return RedirectToAction(nameof(Index)); }

            tx.Commit();
            TempData["Ok"] = maPN == null
                ? $"Đã duyệt: thêm sản phẩm \"{dx.TenSP}\" vào danh mục."
                : $"Đã duyệt: thêm \"{dx.TenSP}\" và nhập {sl} vào kho (phiếu nhập PN-{maPN:00000}).";
        }
        catch (Exception ex) { TempData["Err"] = "Không duyệt được: " + ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TuChoi(int maDX, string? lyDo)
    {
        lyDo = string.IsNullOrWhiteSpace(lyDo) ? null : lyDo.Trim();
        if (lyDo != null && lyDo.Length > 200) lyDo = lyDo[..200];
        await using var c = db.Open();
        var n = await c.ExecuteAsync(
            "UPDATE DeXuatSanPham SET TrangThai = N'TuChoi', LyDoTuChoi = @lyDo, NgayDuyet = GETDATE() WHERE MaDX = @maDX AND TrangThai = N'ChoDuyet'",
            new { lyDo, maDX });
        if (n > 0) TempData["Ok"] = "Đã từ chối đề xuất.";
        else TempData["Err"] = "Đề xuất không còn ở trạng thái chờ duyệt.";
        return RedirectToAction(nameof(Index));
    }
}
