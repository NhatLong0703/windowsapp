using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    // Trang riêng cho nhà cung cấp: chỉ xem, và chỉ thấy dữ liệu của chính mình.
    [Authorize(Roles = "NhaCungCap")]
    public class NccPortalController(Db db) : AppController
    {
        public async Task<IActionResult> Index()
        {
            var ncc = User.MaNCC();
            if (ncc <= 0) return Forbid();

            await using var c = db.Open();
            var vm = new NccPortalVM
            {
                Ten = await c.ExecuteScalarAsync<string?>("SELECT TenNCC FROM NhaCungCap WHERE MaNCC=@ncc", new { ncc }) ?? "",
                // Chỉ sản phẩm của chính nhà cung cấp này, kèm hàng đang nằm ở kho nào
                Sps = (await c.QueryAsync<NccSpRow>(
                    $@"SELECT s.MaSP, s.TenSP, s.DonViTinh, s.DonGia,
                              ISNULL((SELECT SUM(CASE WHEN t.Ton > 0 THEN t.Ton ELSE 0 END) FROM ({Sql.Stock}) t
                                      WHERE t.MaSP = s.MaSP), 0) AS TongTon,
                              (SELECT STRING_AGG(t.TenKho + N' (' + CAST(t.Ton AS NVARCHAR(12)) + N')', N', ') FROM ({Sql.Stock}) t
                               WHERE t.MaSP = s.MaSP AND t.Ton > 0) AS TonTheoKho
                       FROM SanPham s WHERE s.MaNCC = @ncc ORDER BY s.TenSP", new { ncc })).ToList(),
                Phieus = (await c.QueryAsync<NccPhieuRow>(
                    @"SELECT p.MaPN, p.NgayNhap, k.TenKho, p.GhiChu,
                             COUNT(ct.MaCTPN) AS SoDong,
                             ISNULL(SUM(ct.SoLuong * ct.DonGiaNhap), 0) AS TongTien
                      FROM PhieuNhap p
                      JOIN Kho k ON k.MaKho = p.MaKho
                      LEFT JOIN ChiTietPhieuNhap ct ON ct.MaPN = p.MaPN
                      WHERE p.MaNCC = @ncc
                      GROUP BY p.MaPN, p.NgayNhap, k.TenKho, p.GhiChu
                      ORDER BY p.NgayNhap DESC, p.MaPN DESC", new { ncc })).ToList()
            };
            return View(vm);
        }
    }
}

namespace WebApplication1.Models
{
    public class NccSpRow
    {
        public int MaSP { get; set; }
        public string TenSP { get; set; } = "";
        public string? DonViTinh { get; set; }
        public decimal? DonGia { get; set; }
        public int TongTon { get; set; }
        public string? TonTheoKho { get; set; }   // vd: "Kho 1 (20), Kho 2 (5)"
    }

    public class NccPhieuRow
    {
        public int MaPN { get; set; }
        public DateTime NgayNhap { get; set; }
        public string TenKho { get; set; } = "";
        public string? GhiChu { get; set; }
        public int SoDong { get; set; }
        public decimal TongTien { get; set; }
    }

    public class NccPortalVM
    {
        public string Ten { get; set; } = "";
        public List<NccSpRow> Sps { get; set; } = new();
        public List<NccPhieuRow> Phieus { get; set; } = new();
    }
}