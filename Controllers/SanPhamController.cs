using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class SanPhamController(Db db) : StaffController(db)
{
    private class SpNccRow
    {
        public int MaSP { get; set; }
        public int? MaNCC { get; set; }
    }

    public async Task<IActionResult> Index(string? q, int? maLoai)
    {
        await using var c = db.Open();
        var items = (await c.QueryAsync<SanPham>(
            $@"SELECT s.*, l.TenLoai,
                      ISNULL((SELECT SUM(CASE WHEN t.Ton > 0 THEN t.Ton ELSE 0 END) FROM ({Sql.Stock}) t
                              WHERE t.MaSP = s.MaSP AND (@k IS NULL OR t.MaKho = @k)), 0) AS TongTon,
                      (SELECT STRING_AGG(t.TenKho + N' (' + CAST(t.Ton AS NVARCHAR(12)) + N')', N', ') FROM ({Sql.Stock}) t
                       WHERE t.MaSP = s.MaSP AND t.Ton > 0 AND (@k IS NULL OR t.MaKho = @k)) AS TonTheoKho
               FROM SanPham s LEFT JOIN LoaiHang l ON l.MaLoai = s.MaLoai
               WHERE (@q IS NULL OR s.TenSP LIKE @q OR s.MoTa LIKE @q) AND (@l IS NULL OR s.MaLoai = @l)
               ORDER BY s.TenSP", new { q = Like(q), l = maLoai, k = MyKho })).ToList();
        ViewBag.Loai = (await c.QueryAsync<LoaiHang>("SELECT * FROM LoaiHang ORDER BY TenLoai")).ToList();

        // Nhà cung cấp của từng sản phẩm: ViewBag.SpNcc[MaSP] = MaNCC (hoặc null nếu chưa gán)
        ViewBag.SpNcc = (await c.QueryAsync<SpNccRow>("SELECT MaSP, MaNCC FROM SanPham"))
            .ToDictionary(x => x.MaSP, x => x.MaNCC);
        ViewBag.Nccs = (await c.QueryAsync<NhaCungCap>("SELECT MaNCC, TenNCC FROM NhaCungCap ORDER BY TenNCC")).ToList();

        ViewBag.Q = q; ViewBag.MaLoai = maLoai;
        return View(items);
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save(SanPham m, int? maNCC)
    {
        // Không thêm sản phẩm trực tiếp: sản phẩm mới đến từ đề xuất của nhà cung cấp (DeXuatController.Duyet). Ở đây chỉ sửa sản phẩm có sẵn.
        if (m.MaSP == 0)
        {
            FlashErr("Sản phẩm mới do nhà cung cấp đề xuất; hãy duyệt ở mục Duyệt đề xuất.");
            return RedirectToAction(nameof(Index));
        }
        if (!Valid()) return RedirectToAction(nameof(Index));

        int? ncc = maNCC is > 0 ? maNCC : null;
        // Nếu form không gửi ô nhà cung cấp thì giữ nguyên nhà cung cấp cũ, không ghi đè thành rỗng
        var coNcc = Request.Form.ContainsKey("maNCC") ? 1 : 0;

        await Try(async () =>
        {
            await using var c = db.Open();
            await c.ExecuteAsync(@"UPDATE SanPham SET TenSP=@TenSP, DonViTinh=@DonViTinh, DonGia=@DonGia, MoTa=@MoTa,
                                       MaLoai=@MaLoai, TonToiThieu=@TonToiThieu,
                                       MaNCC = CASE WHEN @CoNcc = 1 THEN @MaNCC ELSE MaNCC END
                                       WHERE MaSP=@MaSP",
                    new { m.TenSP, m.DonViTinh, m.DonGia, m.MoTa, m.MaLoai, m.TonToiThieu, m.MaSP, MaNCC = ncc, CoNcc = coNcc });
        }, "Đã lưu sản phẩm.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await Try(async () =>
        {
            await using var c = db.Open();
            await c.ExecuteAsync("DELETE SanPham WHERE MaSP=@id", new { id });
        }, "Đã xóa sản phẩm.");
        return RedirectToAction(nameof(Index));
    }
}