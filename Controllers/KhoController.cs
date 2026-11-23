using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class KhoController(Db db) : StaffController(db)
{
    // Kho + số liệu tồn tổng hợp. @k = null: mọi kho; có giá trị: chỉ kho đó.
    private static readonly string KhoSql = $@"
SELECT k.*,
       (SELECT STRING_AGG(n.HoTen, N', ') FROM NhanVien n JOIN TaiKhoan t ON t.MaNV = n.MaNV AND t.VaiTro = 'ThuKho'
        WHERE n.MaKho = k.MaKho) AS ThuKho,
       ISNULL(s.SoMatHang, 0) AS SoMatHang, ISNULL(s.TongTon, 0) AS TongTon,
       ISNULL(s.GiaTri, 0) AS GiaTri, ISNULL(s.SapHet, 0) AS SapHet
FROM Kho k
LEFT JOIN (SELECT MaKho,
                  SUM(CASE WHEN Ton > 0 THEN 1 ELSE 0 END) AS SoMatHang,
                  SUM(CASE WHEN Ton > 0 THEN Ton ELSE 0 END) AS TongTon,
                  SUM(CASE WHEN Ton > 0 THEN Ton * DonGia ELSE 0 END) AS GiaTri,
                  SUM(CASE WHEN Ton <= TonToiThieu THEN 1 ELSE 0 END) AS SapHet
           FROM ({Sql.Stock}) t GROUP BY MaKho) s ON s.MaKho = k.MaKho
WHERE (@k IS NULL OR k.MaKho = @k)
ORDER BY k.TenKho";

    // Thủ kho chỉ thấy kho của mình; Quản lý thấy tất cả.
    public async Task<IActionResult> Index()
    {
        await using var c = db.Open();
        return View((await c.QueryAsync<KhoRow>(KhoSql, new { k = MyKho })).ToList());
    }

    // Chi tiết một kho: còn bao nhiêu, hàng gì, và các phiếu nhập/xuất gần đây
    public async Task<IActionResult> Detail(int id, string? q, string? tt)
    {
        if (!IsAdmin && id != MyKho) return NotFound();   // thủ kho không xem kho khác
        if (tt is not ("on" or "sap" or "het")) tt = null;
        await using var c = db.Open();
        var kho = await c.QueryFirstOrDefaultAsync<KhoRow>(KhoSql, new { k = (int?)id });
        if (kho == null) return NotFound();

        var all = (await c.QueryAsync<TonRow>(
            $"SELECT * FROM ({Sql.Stock}) t WHERE t.MaKho = @id ORDER BY t.TenSP", new { id })).ToList();
        static string St(TonRow r) => r.Ton <= 0 ? "het" : r.Ton <= r.TonToiThieu ? "sap" : "on";
        ViewBag.CountAll = all.Count;
        ViewBag.CountOn = all.Count(r => St(r) == "on");
        ViewBag.CountSap = all.Count(r => St(r) == "sap");
        ViewBag.CountHet = all.Count(r => St(r) == "het");

        var rows = all.Where(r => (tt == null || St(r) == tt)
                               && (string.IsNullOrWhiteSpace(q) || r.TenSP.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase)))
                      .OrderByDescending(r => r.Ton).ThenBy(r => r.TenSP).ToList();

        ViewBag.Kho = kho; ViewBag.Q = q; ViewBag.Tt = tt;
        ViewBag.Recent = (await c.QueryAsync<PhieuRow>(
            $@"SELECT TOP 8 * FROM ({Sql.PhieuNhapRows} UNION ALL {Sql.PhieuXuatRows}) r
               WHERE r.MaKho = @id ORDER BY r.Ngay DESC, r.Id DESC", new { id })).ToList();
        return View(rows);
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save(Kho m)
    {
        if (!Valid()) return RedirectToAction(nameof(Index));
        await Try(async () =>
        {
            await using var c = db.Open();
            if (m.MaKho == 0)
                await c.ExecuteAsync("INSERT Kho(TenKho, DiaChi, SoDienThoai) VALUES (@TenKho, @DiaChi, @SoDienThoai)", m);
            else
                await c.ExecuteAsync("UPDATE Kho SET TenKho=@TenKho, DiaChi=@DiaChi, SoDienThoai=@SoDienThoai WHERE MaKho=@MaKho", m);
        }, "Đã lưu kho.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await Try(async () =>
        {
            await using var c = db.Open();
            await c.ExecuteAsync("DELETE Kho WHERE MaKho=@id", new { id });
        }, "Đã xóa kho.");
        return RedirectToAction(nameof(Index));
    }
}
