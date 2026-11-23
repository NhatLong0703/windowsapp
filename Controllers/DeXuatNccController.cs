using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

// Cổng nhà cung cấp: gửi đề xuất sản phẩm mới tới quản lý, xem trạng thái, hủy đề xuất đang chờ.
// Chống giả mạo form đã bật toàn cục trong Program.cs nên không cần [ValidateAntiForgeryToken].
[Authorize(Roles = "NhaCungCap")]
public class DeXuatNccController(Db db) : AppController
{
    // Lấy mã nhà cung cấp giống NccPortalController (helper trong Helpers).
    private int MaNCC => User.MaNCC();

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (MaNCC <= 0) { context.Result = Forbid(); return; }
        await next();
    }

    public async Task<IActionResult> Index()
    {
        await using var c = db.Open();
        var rows = (await c.QueryAsync<DeXuatRow>(
            @"SELECT d.MaDX, d.MaNCC, d.TenSP, d.DonViTinh, d.DonGia, d.MoTa, d.MaLoai, l.TenLoai,
                     d.SoLuong, d.MaKho, k.TenKho, d.TrangThai, d.LyDoTuChoi, d.NgayTao, d.NgayDuyet, d.MaSPTao, d.MaPNTao
              FROM DeXuatSanPham d
              LEFT JOIN Kho k ON k.MaKho = d.MaKho
              LEFT JOIN LoaiHang l ON l.MaLoai = d.MaLoai
              WHERE d.MaNCC = @MaNCC
              ORDER BY d.NgayTao DESC, d.MaDX DESC", new { MaNCC })).ToList();
        ViewBag.Khos = (await c.QueryAsync<Kho>("SELECT MaKho, TenKho FROM Kho ORDER BY TenKho")).ToList();
        ViewBag.Loais = (await c.QueryAsync("SELECT MaLoai, TenLoai FROM LoaiHang ORDER BY TenLoai")).ToList();
        return View(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Gui(string? tenSP, string? donViTinh, decimal? donGia,
        string? moTa, int? maLoai, int? soLuong, int? maKho)
    {
        tenSP = tenSP?.Trim();
        donViTinh = string.IsNullOrWhiteSpace(donViTinh) ? null : donViTinh.Trim();
        moTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim();
        var gia = donGia ?? 0;
        var sl = soLuong ?? 0;
        int? kho = maKho is > 0 ? maKho : null;
        int? loai = maLoai is > 0 ? maLoai : null;

        if (string.IsNullOrEmpty(tenSP) || tenSP.Length > 100) { TempData["Err"] = "Tên sản phẩm bắt buộc, tối đa 100 ký tự."; return RedirectToAction(nameof(Index)); }
        if (donViTinh?.Length > 30) { TempData["Err"] = "Đơn vị tối đa 30 ký tự."; return RedirectToAction(nameof(Index)); }
        if (moTa?.Length > 200) { TempData["Err"] = "Mô tả tối đa 200 ký tự."; return RedirectToAction(nameof(Index)); }
        if (gia < 0 || sl < 0) { TempData["Err"] = "Đơn giá và số lượng không được âm."; return RedirectToAction(nameof(Index)); }
        if (sl > 0 && kho == null) { TempData["Err"] = "Muốn giao hàng thì hãy chọn kho nhận."; return RedirectToAction(nameof(Index)); }

        await using var c = db.Open();
        if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SanPham WHERE TenSP = @tenSP", new { tenSP }) > 0)
        { TempData["Err"] = "Sản phẩm trùng tên đã có trong danh mục."; return RedirectToAction(nameof(Index)); }
        if (await c.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM DeXuatSanPham WHERE MaNCC = @MaNCC AND TenSP = @tenSP AND TrangThai = N'ChoDuyet'",
                new { MaNCC, tenSP }) > 0)
        { TempData["Err"] = "Bạn đã có đề xuất cùng tên đang chờ duyệt."; return RedirectToAction(nameof(Index)); }

        await c.ExecuteAsync(
            @"INSERT DeXuatSanPham(MaNCC, TenSP, DonViTinh, DonGia, MoTa, MaLoai, SoLuong, MaKho)
              VALUES (@MaNCC, @tenSP, @donViTinh, @gia, @moTa, @loai, @sl, @kho)",
            new { MaNCC, tenSP, donViTinh, gia, moTa, loai, sl, kho });
        TempData["Ok"] = "Đã gửi đề xuất, chờ quản lý duyệt.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Huy(int maDX)
    {
        await using var c = db.Open();
        var n = await c.ExecuteAsync(
            "UPDATE DeXuatSanPham SET TrangThai = N'DaHuy' WHERE MaDX = @maDX AND MaNCC = @MaNCC AND TrangThai = N'ChoDuyet'",
            new { maDX, MaNCC });
        if (n > 0) TempData["Ok"] = "Đã hủy đề xuất."; else TempData["Err"] = "Không hủy được đề xuất này.";
        return RedirectToAction(nameof(Index));
    }
}
