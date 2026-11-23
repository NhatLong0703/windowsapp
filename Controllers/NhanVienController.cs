using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Admin")]
public class NhanVienController(Db db) : StaffController(db)
{
    public async Task<IActionResult> Index()
    {
        await using var c = db.Open();
        ViewBag.Khos = (await c.QueryAsync<Kho>("SELECT * FROM Kho ORDER BY TenKho")).ToList();
        return View((await c.QueryAsync<NhanVien>(
            @"SELECT n.*, t.TenDangNhap, t.VaiTro, k.TenKho FROM NhanVien n
              LEFT JOIN TaiKhoan t ON t.MaNV = n.MaNV
              LEFT JOIN Kho k ON k.MaKho = n.MaKho ORDER BY n.HoTen")).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Save(NhanVien m)
    {
        if (!Valid()) return RedirectToAction(nameof(Index));

        var vaiTro = m.VaiTro == Roles.Admin ? Roles.Admin : Roles.ThuKho;
        if (m.MaNV != 0 && m.MaNV == User.MaNV()) vaiTro = Roles.Admin;   // không tự hạ quyền chính mình
        if (vaiTro == Roles.ThuKho && !string.IsNullOrWhiteSpace(m.TenDangNhap) && m.MaKho == null)
        {
            FlashErr("Thủ kho phải được gán đúng một kho. Hãy chọn kho phụ trách.");
            return RedirectToAction(nameof(Index));
        }
        m.MaKho = vaiTro == Roles.ThuKho ? m.MaKho : null;   // Quản lý không gắn kho
        m.ChucVu = Roles.Label(vaiTro);   // chức vụ lấy theo vai trò, không nhập riêng nữa

        await Try(async () =>
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();

            if (m.MaNV == 0)
                m.MaNV = await c.ExecuteScalarAsync<int>(
                    @"INSERT NhanVien(HoTen, GioiTinh, NgaySinh, SoDienThoai, DiaChi, ChucVu, MaKho)
                      VALUES (@HoTen, @GioiTinh, @NgaySinh, @SoDienThoai, @DiaChi, @ChucVu, @MaKho);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);", m, tx);
            else
                await c.ExecuteAsync(
                    @"UPDATE NhanVien SET HoTen=@HoTen, GioiTinh=@GioiTinh, NgaySinh=@NgaySinh, SoDienThoai=@SoDienThoai,
                      DiaChi=@DiaChi, ChucVu=@ChucVu, MaKho=@MaKho WHERE MaNV=@MaNV", m, tx);

            if (!string.IsNullOrWhiteSpace(m.TenDangNhap))
            {
                var tk = await c.QueryFirstOrDefaultAsync<int?>("SELECT MaTK FROM TaiKhoan WHERE MaNV=@MaNV", new { m.MaNV }, tx);
                var ten = m.TenDangNhap.Trim();
                if (tk == null)
                {
                    if (string.IsNullOrEmpty(m.MatKhau) || m.MatKhau.Length < 6) return "Cần nhập mật khẩu (ít nhất 6 ký tự) khi tạo tài khoản.";
                    await c.ExecuteAsync("INSERT TaiKhoan(TenDangNhap, MatKhauHash, VaiTro, MaNV) VALUES (@ten, @h, @vaiTro, @MaNV)",
                        new { ten, h = Pw.Hash(m.MatKhau), vaiTro, m.MaNV }, tx);
                }
                else
                {
                    await c.ExecuteAsync("UPDATE TaiKhoan SET TenDangNhap=@ten, VaiTro=@vaiTro WHERE MaTK=@tk", new { ten, vaiTro, tk }, tx);
                    if (!string.IsNullOrEmpty(m.MatKhau))
                    {
                        if (m.MatKhau.Length < 6) return "Mật khẩu mới cần ít nhất 6 ký tự.";
                        await c.ExecuteAsync("UPDATE TaiKhoan SET MatKhauHash=@h WHERE MaTK=@tk", new { h = Pw.Hash(m.MatKhau), tk }, tx);
                    }
                }
            }
            tx.Commit();
            return null;
        }, "Đã lưu nhân viên.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (id == User.MaNV()) { FlashErr("Bạn không thể xóa chính tài khoản đang đăng nhập."); return RedirectToAction(nameof(Index)); }
        await Try(async () =>
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();
            await c.ExecuteAsync("DELETE TaiKhoan WHERE MaNV=@id", new { id }, tx);
            await c.ExecuteAsync("DELETE NhanVien WHERE MaNV=@id", new { id }, tx);
            tx.Commit();
        }, "Đã xóa nhân viên.");
        return RedirectToAction(nameof(Index));
    }
}
