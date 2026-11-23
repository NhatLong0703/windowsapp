using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

// Nhà cung cấp nhận các yêu cầu nhập hàng đã được admin duyệt. Xác nhận giao -> tự tạo phiếu nhập, hàng vào kho.
[Authorize(Roles = "NhaCungCap")]
public class YeuCauNccController(Db db) : AppController
{
    private int MaNCC => User.MaNCC();

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (MaNCC <= 0) { context.Result = Forbid(); return; }
        await next();
    }

    public async Task<IActionResult> Index()
    {
        await using var c = db.Open();
        var rows = (await c.QueryAsync<YeuCauRow>(
            $@"SELECT * FROM ({YcSql.Rows}) r
               WHERE r.MaNCC = @MaNCC AND r.TrangThai IN (N'ChoNCC', N'DaGiao', N'NCCTuChoi')
               ORDER BY CASE WHEN r.TrangThai = N'ChoNCC' THEN 0 ELSE 1 END, r.NgayTao DESC, r.MaYC DESC", new { MaNCC })).ToList();
        return View(rows);
    }

    [HttpPost]
    public async Task<IActionResult> XacNhan(int id)
    {
        try
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();
            var yc = await c.QueryFirstOrDefaultAsync<(int MaKho, int MaSP, int SoLuong, int? MaNVDuyet)>(
                @"SELECT MaKho, MaSP, SoLuong, MaNVDuyet FROM YeuCauNhap WITH (UPDLOCK, ROWLOCK)
                  WHERE MaYC = @id AND MaNCC = @MaNCC AND TrangThai = N'ChoNCC'", new { id, MaNCC }, tx);
            if (yc.SoLuong <= 0 || yc.MaNVDuyet == null)
            {
                tx.Rollback();
                FlashErr("Yêu cầu không còn ở trạng thái chờ giao.");
                return RedirectToAction(nameof(Index));
            }
            var gia = await c.ExecuteScalarAsync<decimal>("SELECT ISNULL(DonGia, 0) FROM SanPham WHERE MaSP = @MaSP", new { yc.MaSP }, tx);
            var maPN = await c.ExecuteScalarAsync<int>(
                @"INSERT PhieuNhap(MaNV, MaKho, MaNCC, NgayNhap, GhiChu)
                  VALUES (@MaNV, @MaKho, @MaNCC, CAST(GETDATE() AS DATE), @GhiChu);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { MaNV = yc.MaNVDuyet, yc.MaKho, MaNCC, GhiChu = "Từ yêu cầu nhập hàng #" + id }, tx);
            await c.ExecuteAsync(
                "INSERT ChiTietPhieuNhap(MaPN, MaSP, SoLuong, DonGiaNhap) VALUES (@maPN, @MaSP, @SoLuong, @gia)",
                new { maPN, yc.MaSP, yc.SoLuong, gia }, tx);
            await c.ExecuteAsync(
                "UPDATE YeuCauNhap SET TrangThai = N'DaGiao', NgayGiao = GETDATE(), MaPNTao = @maPN WHERE MaYC = @id",
                new { maPN, id }, tx);
            tx.Commit();
            FlashOk($"Đã xác nhận giao hàng, phiếu nhập PN-{maPN:00000} đã cộng hàng vào kho.");
        }
        catch (SqlException ex) { FlashErr(SqlMsg(ex)); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TuChoi(int id, string? lyDo)
    {
        lyDo = string.IsNullOrWhiteSpace(lyDo) ? null : lyDo.Trim();
        if (lyDo?.Length > 200) lyDo = lyDo[..200];
        await using var c = db.Open();
        var n = await c.ExecuteAsync(
            "UPDATE YeuCauNhap SET TrangThai = N'NCCTuChoi', LyDoTuChoi = @lyDo WHERE MaYC = @id AND MaNCC = @MaNCC AND TrangThai = N'ChoNCC'",
            new { id, lyDo, MaNCC });
        if (n > 0) FlashOk("Đã từ chối yêu cầu."); else FlashErr("Yêu cầu không còn ở trạng thái chờ giao.");
        return RedirectToAction(nameof(Index));
    }
}
