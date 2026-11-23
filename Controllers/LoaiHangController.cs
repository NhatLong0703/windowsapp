using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Admin")]
public class LoaiHangController(Db db) : StaffController(db)
{
    public async Task<IActionResult> Index()
    {
        await using var c = db.Open();
        return View((await c.QueryAsync<LoaiHang>(
            @"SELECT l.*, (SELECT COUNT(*) FROM SanPham s WHERE s.MaLoai = l.MaLoai) AS SoSP
              FROM LoaiHang l ORDER BY l.TenLoai")).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Save(LoaiHang m)
    {
        if (!Valid()) return RedirectToAction(nameof(Index));
        await Try(async () =>
        {
            await using var c = db.Open();
            if (m.MaLoai == 0) await c.ExecuteAsync("INSERT LoaiHang(TenLoai) VALUES (@TenLoai)", m);
            else await c.ExecuteAsync("UPDATE LoaiHang SET TenLoai=@TenLoai WHERE MaLoai=@MaLoai", m);
        }, "Đã lưu loại hàng.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await Try(async () =>
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();
            await c.ExecuteAsync("UPDATE SanPham SET MaLoai = NULL WHERE MaLoai=@id", new { id }, tx);
            await c.ExecuteAsync("DELETE LoaiHang WHERE MaLoai=@id", new { id }, tx);
            tx.Commit();
        }, "Đã xóa loại hàng.");
        return RedirectToAction(nameof(Index));
    }
}
