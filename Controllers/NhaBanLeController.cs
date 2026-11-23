using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Admin")]
public class NhaBanLeController(Db db) : StaffController(db)
{
    public async Task<IActionResult> Index(string? q)
    {
        await using var c = db.Open();
        var items = (await c.QueryAsync<NhaBanLe>(
            @"SELECT b.*, t.TenDangNhap FROM NhaBanLe b LEFT JOIN TaiKhoan t ON t.MaBL = b.MaBL
              WHERE (@q IS NULL OR b.TenBL LIKE @q OR b.NguoiLienHe LIKE @q OR b.SoDienThoai LIKE @q OR t.TenDangNhap LIKE @q)
              ORDER BY b.TenBL", new { q = Like(q) })).ToList();
        ViewBag.Q = q;
        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> Save(NhaBanLe m)
    {
        if (!Valid()) return RedirectToAction(nameof(Index));
        await Try(async () =>
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();

            if (m.MaBL == 0)
                m.MaBL = await c.ExecuteScalarAsync<int>(
                    @"INSERT NhaBanLe(TenBL, NguoiLienHe, SoDienThoai, DiaChi, Email) VALUES (@TenBL, @NguoiLienHe, @SoDienThoai, @DiaChi, @Email);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);", m, tx);
            else
                await c.ExecuteAsync(
                    "UPDATE NhaBanLe SET TenBL=@TenBL, NguoiLienHe=@NguoiLienHe, SoDienThoai=@SoDienThoai, DiaChi=@DiaChi, Email=@Email WHERE MaBL=@MaBL", m, tx);

            if (!string.IsNullOrWhiteSpace(m.TenDangNhap))
            {
                var tk = await c.QueryFirstOrDefaultAsync<int?>("SELECT MaTK FROM TaiKhoan WHERE MaBL=@MaBL", new { m.MaBL }, tx);
                var ten = m.TenDangNhap.Trim();
                if (tk == null)
                {
                    if (string.IsNullOrEmpty(m.MatKhau) || m.MatKhau.Length < 6) return "Cần nhập mật khẩu (ít nhất 6 ký tự) khi tạo tài khoản.";
                    await c.ExecuteAsync("INSERT TaiKhoan(TenDangNhap, MatKhauHash, VaiTro, MaBL) VALUES (@ten, @h, 'BanLe', @MaBL)",
                        new { ten, h = Pw.Hash(m.MatKhau), m.MaBL }, tx);
                }
                else
                {
                    await c.ExecuteAsync("UPDATE TaiKhoan SET TenDangNhap=@ten WHERE MaTK=@tk", new { ten, tk }, tx);
                    if (!string.IsNullOrEmpty(m.MatKhau))
                    {
                        if (m.MatKhau.Length < 6) return "Mật khẩu mới cần ít nhất 6 ký tự.";
                        await c.ExecuteAsync("UPDATE TaiKhoan SET MatKhauHash=@h WHERE MaTK=@tk", new { h = Pw.Hash(m.MatKhau), tk }, tx);
                    }
                }
            }
            tx.Commit();
            return null;
        }, "Đã lưu nhà bán lẻ.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await Try(async () =>
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();
            await c.ExecuteAsync("DELETE TaiKhoan WHERE MaBL=@id", new { id }, tx);
            await c.ExecuteAsync("DELETE NhaBanLe WHERE MaBL=@id", new { id }, tx);   // lỗi FK nếu đã có đơn
            tx.Commit();
        }, "Đã xóa nhà bán lẻ.");
        return RedirectToAction(nameof(Index));
    }
}
