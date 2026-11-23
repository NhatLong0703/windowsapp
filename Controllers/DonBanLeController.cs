using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

// Đơn đặt mua của nhà bán lẻ. Thủ kho xác nhận/từ chối đơn của kho mình; Quản lý chỉ xem.
public class DonBanLeController(Db db) : StaffController(db)
{
    public async Task<IActionResult> Index(string? trangThai, string? q)
    {
        if (trangThai is not ("ChoXacNhan" or "DaXacNhan" or "TuChoi" or "DaHuy")) trangThai = null;
        await using var c = db.Open();
        var rows = (await c.QueryAsync<DonRow>(
            $@"SELECT * FROM ({Sql.DonRows}) d
               WHERE (@k IS NULL OR d.MaKho = @k) AND (@tt IS NULL OR d.TrangThai = @tt)
                 AND (@q IS NULL OR d.Ma LIKE @q OR d.TenBL LIKE @q)
               ORDER BY CASE WHEN d.TrangThai = 'ChoXacNhan' THEN 0 ELSE 1 END, d.NgayDat DESC, d.Id DESC",
            new { k = MyKho, tt = trangThai, q = Like(q) })).ToList();
        ViewBag.TrangThai = trangThai; ViewBag.Q = q;
        return View(rows);
    }

    public async Task<IActionResult> Detail(int id)
    {
        await using var c = db.Open();
        var don = await c.QueryFirstOrDefaultAsync<DonRow>(
            $"SELECT * FROM ({Sql.DonRows}) d WHERE d.Id = @id AND (@k IS NULL OR d.MaKho = @k)", new { id, k = MyKho });
        if (don == null) return NotFound();
        var lines = (await c.QueryAsync<DonLine>(
            $@"SELECT c.MaSP, s.TenSP, s.DonViTinh, c.SoLuong, c.DonGia, ISNULL(t.Ton, 0) AS Ton
               FROM ChiTietDonDatMua c JOIN SanPham s ON s.MaSP = c.MaSP
               LEFT JOIN ({Sql.Stock}) t ON t.MaSP = c.MaSP AND t.MaKho = @kho
               WHERE c.MaDon = @id ORDER BY s.TenSP", new { id, kho = don.MaKho })).ToList();
        return View(new DonDetailVM { Don = don, Lines = lines });
    }

    [HttpPost]
    public async Task<IActionResult> XacNhan(int id)
    {
        if (IsAdmin) return Forbid();   // chỉ thủ kho xác nhận
        try
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction();
            var d = await c.QueryFirstOrDefaultAsync<DonHead>(
                @"SELECT d.MaDon, d.MaKho, d.TrangThai, b.TenBL,
                         'DM-' + CAST(YEAR(d.NgayDat) AS VARCHAR(4)) + '-' + RIGHT('0000' + CAST(d.MaDon AS VARCHAR(10)), 4) AS Ma
                  FROM DonDatMua d WITH (UPDLOCK, ROWLOCK) JOIN NhaBanLe b ON b.MaBL = d.MaBL WHERE d.MaDon = @id", new { id }, tx);
            if (d == null || d.MaKho != MyKho) return NotFound();
            if (d.TrangThai != "ChoXacNhan")
            {
                FlashErr("Đơn này đã được xử lý trước đó.");
                return RedirectToAction(nameof(Detail), new { id });
            }

            var negSql = $"SELECT COUNT(*) FROM ({Sql.Stock}) t WHERE Ton < 0";
            var before = await c.ExecuteScalarAsync<int>(negSql, null, tx);

            var px = await c.ExecuteScalarAsync<int>(
                @"INSERT PhieuXuat(MaNV, MaKho, NgayXuat, GhiChu) VALUES (@nv, @k, CAST(GETDATE() AS DATE), @gc);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { nv = User.MaNV(), k = d.MaKho, gc = $"Xuất cho đơn bán lẻ {d.Ma} - {d.TenBL}" }, tx);
            await c.ExecuteAsync(
                @"INSERT ChiTietPhieuXuat(MaPX, MaSP, SoLuong, DonGiaXuat)
                  SELECT @px, MaSP, SoLuong, DonGia FROM ChiTietDonDatMua WHERE MaDon = @id", new { px, id }, tx);

            var after = await c.ExecuteScalarAsync<int>(negSql, null, tx);
            if (after > before)
            {
                var bad = (await c.QueryAsync<string>(
                    $@"SELECT TOP 5 TenSP + N' (còn ' + CAST(Ton + SoLuong AS NVARCHAR(12)) + N', cần ' + CAST(SoLuong AS NVARCHAR(12)) + N')'
                       FROM (SELECT t.TenSP, t.Ton, c.SoLuong FROM ({Sql.Stock}) t
                             JOIN ChiTietDonDatMua c ON c.MaSP = t.MaSP AND c.MaDon = @id WHERE t.MaKho = @k AND t.Ton < 0) x",
                    new { id, k = d.MaKho }, tx)).ToList();
                tx.Rollback();
                FlashErr("Không đủ tồn kho để xác nhận đơn: " + string.Join("; ", bad));
                return RedirectToAction(nameof(Detail), new { id });
            }

            await c.ExecuteAsync(
                "UPDATE DonDatMua SET TrangThai='DaXacNhan', MaNVXuLy=@nv, NgayXuLy=GETDATE(), MaPX=@px WHERE MaDon=@id",
                new { nv = User.MaNV(), px, id }, tx);
            tx.Commit();
            FlashOk($"Đã xác nhận đơn {d.Ma} và tạo phiếu xuất kho tương ứng.");
        }
        catch (SqlException ex) { FlashErr(SqlMsg(ex)); }
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> TuChoi(int id, string? lyDo)
    {
        if (IsAdmin) return Forbid();
        if (string.IsNullOrWhiteSpace(lyDo)) { FlashErr("Hãy nhập lý do từ chối."); return RedirectToAction(nameof(Detail), new { id }); }
        await using var c = db.Open();
        var rows = await c.ExecuteAsync(
            @"UPDATE DonDatMua SET TrangThai='TuChoi', LyDoTuChoi=@ly, MaNVXuLy=@nv, NgayXuLy=GETDATE()
              WHERE MaDon=@id AND MaKho=@k AND TrangThai='ChoXacNhan'",
            new { ly = lyDo.Trim().Length > 200 ? lyDo.Trim()[..200] : lyDo.Trim(), nv = User.MaNV(), id, k = MyKho });
        if (rows == 0) FlashErr("Không thể từ chối: đơn không tồn tại hoặc đã được xử lý.");
        else FlashOk("Đã từ chối đơn.");
        return RedirectToAction(nameof(Detail), new { id });
    }
}
