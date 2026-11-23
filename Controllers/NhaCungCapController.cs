using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class NhaCungCapController(Db db) : StaffController(db)
{
    private class TkNccRow
    {
        public int MaNCC { get; set; }
        public string? TenDangNhap { get; set; }
    }

    public async Task<IActionResult> Index(string? q)
    {
        await using var c = db.Open();
        var items = (await c.QueryAsync<NhaCungCap>(
            @"SELECT n.*,
                     (SELECT COUNT(*) FROM NguoiDaiDien d WHERE d.MaNCC = n.MaNCC) AS SoDaiDien,
                     (SELECT STRING_AGG(l.TenLoai, N', ') FROM NCC_LoaiHang x JOIN LoaiHang l ON l.MaLoai = x.MaLoai WHERE x.MaNCC = n.MaNCC) AS TenLoaiHang
              FROM NhaCungCap n
              WHERE (@q IS NULL OR n.TenNCC LIKE @q OR n.SoDienThoai LIKE @q OR n.Email LIKE @q OR n.DiaChi LIKE @q)
              ORDER BY n.TenNCC", new { q = Like(q) })).ToList();

        // Tên đăng nhập của từng nhà cung cấp: ViewBag.TkNcc[MaNCC] = TenDangNhap
        ViewBag.TkNcc = (await c.QueryAsync<TkNccRow>("SELECT MaNCC, TenDangNhap FROM TaiKhoan WHERE MaNCC IS NOT NULL"))
            .ToDictionary(x => x.MaNCC, x => x.TenDangNhap ?? "");
        ViewBag.Q = q;
        return View(items);
    }

    public async Task<IActionResult> Detail(int id)
    {
        await using var c = db.Open();
        var ncc = await c.QueryFirstOrDefaultAsync<NhaCungCap>("SELECT * FROM NhaCungCap WHERE MaNCC=@id", new { id });
        if (ncc == null) return NotFound();
        var vm = new NccDetailVM
        {
            Ncc = ncc,
            Reps = (await c.QueryAsync<NguoiDaiDien>("SELECT * FROM NguoiDaiDien WHERE MaNCC=@id ORDER BY HoTen", new { id })).ToList(),
            AllLoai = (await c.QueryAsync<LoaiHang>("SELECT * FROM LoaiHang ORDER BY TenLoai")).ToList(),
            Chosen = (await c.QueryAsync<int>("SELECT MaLoai FROM NCC_LoaiHang WHERE MaNCC=@id", new { id })).ToHashSet()
        };
        ViewBag.TenDangNhap = await c.ExecuteScalarAsync<string?>("SELECT TenDangNhap FROM TaiKhoan WHERE MaNCC=@id", new { id });

        // Chỉ các sản phẩm thuộc nhà cung cấp này (SanPham.MaNCC), kèm nơi đang để hàng
        ViewBag.SanPhams = (await c.QueryAsync<SanPham>(
            $@"SELECT s.*, l.TenLoai,
                      ISNULL((SELECT SUM(CASE WHEN t.Ton > 0 THEN t.Ton ELSE 0 END) FROM ({Sql.Stock}) t
                              WHERE t.MaSP = s.MaSP AND (@k IS NULL OR t.MaKho = @k)), 0) AS TongTon,
                      (SELECT STRING_AGG(t.TenKho + N' (' + CAST(t.Ton AS NVARCHAR(12)) + N')', N', ') FROM ({Sql.Stock}) t
                       WHERE t.MaSP = s.MaSP AND t.Ton > 0 AND (@k IS NULL OR t.MaKho = @k)) AS TonTheoKho
               FROM SanPham s LEFT JOIN LoaiHang l ON l.MaLoai = s.MaLoai
               WHERE s.MaNCC = @id ORDER BY s.TenSP", new { id, k = MyKho })).ToList();
        return View(vm);
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save(NhaCungCap m, string? tenDangNhap, string? matKhau)
    {
        if (!Valid()) return RedirectToAction(nameof(Index));
        var id = m.MaNCC;
        await Try(async () =>
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();

            if (m.MaNCC == 0)
                id = await c.ExecuteScalarAsync<int>(
                    @"INSERT NhaCungCap(TenNCC, SoDienThoai, DiaChi, Email) VALUES (@TenNCC, @SoDienThoai, @DiaChi, @Email);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);", m, tx);
            else
                await c.ExecuteAsync("UPDATE NhaCungCap SET TenNCC=@TenNCC, SoDienThoai=@SoDienThoai, DiaChi=@DiaChi, Email=@Email WHERE MaNCC=@MaNCC", m, tx);

            // Tài khoản đăng nhập (tùy chọn): để trống tên đăng nhập thì không tạo / không đổi
            if (!string.IsNullOrWhiteSpace(tenDangNhap))
            {
                var ten = tenDangNhap.Trim();
                var tk = await c.QueryFirstOrDefaultAsync<int?>("SELECT MaTK FROM TaiKhoan WHERE MaNCC=@id", new { id }, tx);

                var trung = await c.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM TaiKhoan WHERE TenDangNhap=@ten AND (MaNCC IS NULL OR MaNCC<>@id)", new { ten, id }, tx);
                if (trung > 0) return "Tên đăng nhập này đã được sử dụng, hãy chọn tên khác.";

                if (tk == null)
                {
                    if (string.IsNullOrEmpty(matKhau) || matKhau.Length < 6) return "Cần nhập mật khẩu (ít nhất 6 ký tự) khi tạo tài khoản.";
                    await c.ExecuteAsync("INSERT TaiKhoan(TenDangNhap, MatKhauHash, VaiTro, MaNCC) VALUES (@ten, @h, 'NhaCungCap', @id)",
                        new { ten, h = Pw.Hash(matKhau), id }, tx);
                }
                else
                {
                    await c.ExecuteAsync("UPDATE TaiKhoan SET TenDangNhap=@ten WHERE MaTK=@tk", new { ten, tk }, tx);
                    if (!string.IsNullOrEmpty(matKhau))
                    {
                        if (matKhau.Length < 6) return "Mật khẩu mới cần ít nhất 6 ký tự.";
                        await c.ExecuteAsync("UPDATE TaiKhoan SET MatKhauHash=@h WHERE MaTK=@tk", new { h = Pw.Hash(matKhau), tk }, tx);
                    }
                }
            }
            tx.Commit();
            return null;
        }, "Đã lưu đối tác.");
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await Try(async () =>
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();
            await c.ExecuteAsync("DELETE TaiKhoan WHERE MaNCC=@id", new { id }, tx);
            await c.ExecuteAsync("UPDATE SanPham SET MaNCC = NULL WHERE MaNCC=@id", new { id }, tx);
            await c.ExecuteAsync("DELETE NhaCungCap WHERE MaNCC=@id", new { id }, tx);   // lỗi FK nếu đã có phiếu nhập
            tx.Commit();
        }, "Đã xóa đối tác.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetLoai(int id, int[]? maLoai)
    {
        await Try(async () =>
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();
            await c.ExecuteAsync("DELETE NCC_LoaiHang WHERE MaNCC=@id", new { id }, tx);
            if (maLoai is { Length: > 0 })
                await c.ExecuteAsync("INSERT NCC_LoaiHang(MaNCC, MaLoai) VALUES (@id, @l)",
                    maLoai.Distinct().Select(l => new { id, l }), tx);
            tx.Commit();
        }, "Đã cập nhật loại mặt hàng của đối tác.");
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveDaiDien(NguoiDaiDien m)
    {
        if (!Valid()) return RedirectToAction(nameof(Detail), new { id = m.MaNCC });
        await Try(async () =>
        {
            await using var c = db.Open();
            if (m.MaDD == 0)
                await c.ExecuteAsync("INSERT NguoiDaiDien(MaNCC, HoTen, ChucVu, SoDienThoai, Email) VALUES (@MaNCC, @HoTen, @ChucVu, @SoDienThoai, @Email)", m);
            else
                await c.ExecuteAsync("UPDATE NguoiDaiDien SET HoTen=@HoTen, ChucVu=@ChucVu, SoDienThoai=@SoDienThoai, Email=@Email WHERE MaDD=@MaDD AND MaNCC=@MaNCC", m);
        }, "Đã lưu người đại diện.");
        return RedirectToAction(nameof(Detail), new { id = m.MaNCC });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDaiDien(int id, int maNCC)
    {
        await Try(async () =>
        {
            await using var c = db.Open();
            await c.ExecuteAsync("DELETE NguoiDaiDien WHERE MaDD=@id AND MaNCC=@maNCC", new { id, maNCC });
        }, "Đã xóa người đại diện.");
        return RedirectToAction(nameof(Detail), new { id = maNCC });
    }
}