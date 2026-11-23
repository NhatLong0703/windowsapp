using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

// Yêu cầu nhập hàng: Thủ kho tạo (cho kho của mình) -> Admin xem tồn kho rồi duyệt/từ chối -> gửi sang nhà cung cấp.
public class YeuCauNhapController(Db db) : StaffController(db)
{
    // maSP, maKho: điền sẵn form của admin (khi bấm "Nhập kho" ở trang Sản phẩm hoặc Kho)
    public async Task<IActionResult> Index(int? maSP = null, int? maKho = null)
    {
        await using var c = db.Open();
        var rows = (await c.QueryAsync<YeuCauRow>(
            $@"SELECT * FROM ({YcSql.Rows}) r WHERE (@k IS NULL OR r.MaKho = @k)
               ORDER BY CASE WHEN r.TrangThai = N'ChoAdmin' THEN 0 ELSE 1 END, r.NgayTao DESC, r.MaYC DESC", new { k = MyKho })).ToList();
        if (!IsAdmin)
        {
            ViewBag.Sps = (await c.QueryAsync<SpChonRow>(
                $@"SELECT s.MaSP, s.TenSP, s.DonViTinh, ISNULL(s.TonToiThieu, 0) AS TonToiThieu, n.TenNCC, ISNULL(t.Ton, 0) AS Ton
                   FROM SanPham s JOIN NhaCungCap n ON n.MaNCC = s.MaNCC
                   LEFT JOIN ({Sql.Stock}) t ON t.MaSP = s.MaSP AND t.MaKho = @k
                   ORDER BY s.TenSP", new { k = MyKho })).ToList();
        }
        else
        {
            // Form admin: nhà cung cấp -> sản phẩm của nhà cung cấp đó -> kho nhận (kèm tồn hiện tại để tham khảo)
            ViewBag.Nccs = (await c.QueryAsync<NhaCungCap>("SELECT MaNCC, TenNCC FROM NhaCungCap ORDER BY TenNCC")).ToList();
            ViewBag.Khos = (await c.QueryAsync<Kho>("SELECT MaKho, TenKho FROM Kho ORDER BY TenKho")).ToList();
            var sps = (await c.QueryAsync<SpNccChon>(
                "SELECT MaSP, TenSP, DonViTinh, MaNCC FROM SanPham WHERE MaNCC IS NOT NULL ORDER BY TenSP")).ToList();
            ViewBag.SpJson = JsonSerializer.Serialize(sps.Select(s => new { id = s.MaSP, ten = s.TenSP, dvt = s.DonViTinh, ncc = s.MaNCC }));
            var ton = (await c.QueryAsync<TonRow>($"SELECT * FROM ({Sql.Stock}) t")).ToList();
            ViewBag.TonJson = JsonSerializer.Serialize(ton.Select(t => new { k = t.MaKho, s = t.MaSP, t = t.Ton }));
            var pre = maSP == null ? null : sps.FirstOrDefault(s => s.MaSP == maSP);
            ViewBag.PreNcc = pre?.MaNCC; ViewBag.PreSp = pre?.MaSP; ViewBag.PreKho = maKho;
        }
        return View(rows);
    }

    // Admin gửi yêu cầu nhập thẳng tới nhà cung cấp (không qua bước duyệt). Nhà cung cấp xác nhận giao -> tự tạo phiếu nhập vào kho.
    [HttpPost]
    public async Task<IActionResult> TaoNcc(int maNCC, int maSP, int maKho, int soLuong, string? ghiChu)
    {
        if (!IsAdmin) return Forbid();
        if (maNCC <= 0 || maSP <= 0 || maKho <= 0 || soLuong <= 0)
        { FlashErr("Hãy chọn nhà cung cấp, sản phẩm, kho nhận và số lượng lớn hơn 0."); return RedirectToAction(nameof(Index)); }
        ghiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
        if (ghiChu?.Length > 200) ghiChu = ghiChu[..200];

        await using var c = db.Open();
        if (await c.ExecuteScalarAsync<int?>("SELECT MaNCC FROM SanPham WHERE MaSP = @maSP", new { maSP }) != maNCC)
        { FlashErr("Sản phẩm không thuộc nhà cung cấp đã chọn."); return RedirectToAction(nameof(Index)); }
        if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Kho WHERE MaKho = @maKho", new { maKho }) == 0)
        { FlashErr("Kho không tồn tại."); return RedirectToAction(nameof(Index)); }
        if (await c.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM YeuCauNhap WHERE MaKho = @maKho AND MaSP = @maSP AND TrangThai IN (N'ChoAdmin', N'ChoNCC')", new { maKho, maSP }) > 0)
        { FlashErr("Sản phẩm này đã có yêu cầu nhập đang xử lý ở kho đã chọn."); return RedirectToAction(nameof(Index)); }

        await c.ExecuteAsync(
            @"INSERT YeuCauNhap(MaKho, MaSP, MaNCC, SoLuong, GhiChu, MaNVTao, TrangThai, MaNVDuyet, NgayDuyet)
              VALUES (@maKho, @maSP, @maNCC, @soLuong, @ghiChu, @nv, N'ChoNCC', @nv, GETDATE())",
            new { maKho, maSP, maNCC, soLuong, ghiChu, nv = User.MaNV() });
        FlashOk("Đã gửi yêu cầu tới nhà cung cấp. Khi họ xác nhận giao, hàng sẽ vào kho đã chọn.");
        return RedirectToAction(nameof(Index));
    }

    // Thủ kho tạo yêu cầu cho kho của mình
    [HttpPost]
    public async Task<IActionResult> Tao(int maSP, int soLuong, string? ghiChu)
    {
        if (IsAdmin) return Forbid();
        if (maSP <= 0 || soLuong <= 0) { FlashErr("Hãy chọn sản phẩm và số lượng lớn hơn 0."); return RedirectToAction(nameof(Index)); }
        ghiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
        if (ghiChu?.Length > 200) ghiChu = ghiChu[..200];

        await using var c = db.Open();
        var ncc = await c.ExecuteScalarAsync<int?>("SELECT MaNCC FROM SanPham WHERE MaSP = @maSP", new { maSP });
        if (ncc == null) { FlashErr("Sản phẩm này chưa có nhà cung cấp nên không thể yêu cầu nhập."); return RedirectToAction(nameof(Index)); }
        if (await c.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM YeuCauNhap WHERE MaKho = @k AND MaSP = @maSP AND TrangThai IN (N'ChoAdmin', N'ChoNCC')",
                new { k = MyKho, maSP }) > 0)
        { FlashErr("Sản phẩm này đã có yêu cầu nhập đang xử lý ở kho của bạn."); return RedirectToAction(nameof(Index)); }

        await c.ExecuteAsync(
            @"INSERT YeuCauNhap(MaKho, MaSP, MaNCC, SoLuong, GhiChu, MaNVTao) VALUES (@k, @maSP, @ncc, @soLuong, @ghiChu, @nv)",
            new { k = MyKho, maSP, ncc, soLuong, ghiChu, nv = User.MaNV() });
        FlashOk("Đã gửi yêu cầu nhập hàng tới quản lý.");
        return RedirectToAction(nameof(Index));
    }

    // Thủ kho hủy yêu cầu của kho mình khi admin chưa duyệt
    [HttpPost]
    public async Task<IActionResult> Huy(int id)
    {
        if (IsAdmin) return Forbid();
        await using var c = db.Open();
        var n = await c.ExecuteAsync(
            "UPDATE YeuCauNhap SET TrangThai = N'DaHuy' WHERE MaYC = @id AND MaKho = @k AND TrangThai = N'ChoAdmin'", new { id, k = MyKho });
        if (n > 0) FlashOk("Đã hủy yêu cầu."); else FlashErr("Không hủy được: yêu cầu đã được xử lý.");
        return RedirectToAction(nameof(Index));
    }

    // Admin duyệt: chuyển sang nhà cung cấp (có thể chỉnh số lượng)
    [HttpPost]
    public async Task<IActionResult> Duyet(int id, int? soLuong)
    {
        if (!IsAdmin) return Forbid();
        if (soLuong is <= 0) { FlashErr("Số lượng phải lớn hơn 0."); return RedirectToAction(nameof(Index)); }
        await using var c = db.Open();
        var n = await c.ExecuteAsync(
            @"UPDATE YeuCauNhap SET TrangThai = N'ChoNCC', MaNVDuyet = @nv, NgayDuyet = GETDATE(), SoLuong = ISNULL(@soLuong, SoLuong)
              WHERE MaYC = @id AND TrangThai = N'ChoAdmin'", new { id, soLuong, nv = User.MaNV() });
        if (n > 0) FlashOk("Đã duyệt, yêu cầu được gửi tới nhà cung cấp."); else FlashErr("Yêu cầu không còn ở trạng thái chờ duyệt.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TuChoi(int id, string? lyDo)
    {
        if (!IsAdmin) return Forbid();
        lyDo = string.IsNullOrWhiteSpace(lyDo) ? null : lyDo.Trim();
        if (lyDo?.Length > 200) lyDo = lyDo[..200];
        await using var c = db.Open();
        var n = await c.ExecuteAsync(
            @"UPDATE YeuCauNhap SET TrangThai = N'TuChoi', LyDoTuChoi = @lyDo, MaNVDuyet = @nv, NgayDuyet = GETDATE()
              WHERE MaYC = @id AND TrangThai = N'ChoAdmin'", new { id, lyDo, nv = User.MaNV() });
        if (n > 0) FlashOk("Đã từ chối yêu cầu."); else FlashErr("Yêu cầu không còn ở trạng thái chờ duyệt.");
        return RedirectToAction(nameof(Index));
    }
}
