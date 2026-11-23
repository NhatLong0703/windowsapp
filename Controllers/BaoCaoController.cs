using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class BaoCaoController(Db db) : StaffController(db)
{
    private async Task<(DateTime tu, DateTime den, int? kho, List<BaoCaoRow> rows)> Load(DateTime? tu, DateTime? den, int? maKho)
    {
        var t = (tu ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
        var d = (den ?? DateTime.Today).Date;
        int? k = IsAdmin ? maKho : MyKho;   // thủ kho luôn bị giới hạn trong kho của mình
        await using var c = db.Open();
        var rows = (await c.QueryAsync<BaoCaoRow>(
            @"SELECT s.MaSP, s.TenSP, s.DonViTinh,
                     ISNULL(n.SL,0) AS NhapSL, ISNULL(n.GT,0) AS NhapGT, ISNULL(x.SL,0) AS XuatSL, ISNULL(x.GT,0) AS XuatGT
              FROM SanPham s
              LEFT JOIN (SELECT c.MaSP, SUM(c.SoLuong) SL, SUM(c.SoLuong * c.DonGiaNhap) GT
                         FROM PhieuNhap p JOIN ChiTietPhieuNhap c ON c.MaPN = p.MaPN
                         WHERE p.NgayNhap >= @t AND p.NgayNhap <= @d AND (@k IS NULL OR p.MaKho = @k) GROUP BY c.MaSP) n ON n.MaSP = s.MaSP
              LEFT JOIN (SELECT c.MaSP, SUM(c.SoLuong) SL, SUM(c.SoLuong * c.DonGiaXuat) GT
                         FROM PhieuXuat p JOIN ChiTietPhieuXuat c ON c.MaPX = p.MaPX
                         WHERE p.NgayXuat >= @t AND p.NgayXuat <= @d AND (@k IS NULL OR p.MaKho = @k) GROUP BY c.MaSP) x ON x.MaSP = s.MaSP
              WHERE n.SL IS NOT NULL OR x.SL IS NOT NULL
              ORDER BY s.TenSP", new { t, d, k })).ToList();
        return (t, d, k, rows);
    }

    public async Task<IActionResult> Index(DateTime? tu, DateTime? den, int? maKho)
    {
        var (t, d, k, rows) = await Load(tu, den, maKho);
        await using var c = db.Open();
        ViewBag.Tu = t; ViewBag.Den = d; ViewBag.MaKho = k;
        ViewBag.Khos = (await c.QueryAsync<Kho>("SELECT * FROM Kho WHERE (@k IS NULL OR MaKho = @k) ORDER BY TenKho", new { k = MyKho })).ToList();
        return View(rows);
    }

    public async Task<IActionResult> Csv(DateTime? tu, DateTime? den, int? maKho)
    {
        var (t, d, _, rows) = await Load(tu, den, maKho);
        static string Esc(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine("Mã SP,Sản phẩm,Đơn vị,SL nhập,Giá trị nhập,SL xuất,Giá trị xuất");
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", new[]
            {
                Esc("SP-" + r.MaSP.ToString("00000")), Esc(r.TenSP), Esc(r.DonViTinh),
                r.NhapSL.ToString(), r.NhapGT.ToString("0"), r.XuatSL.ToString(), r.XuatGT.ToString("0")
            }));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"bao-cao-{t:yyyyMMdd}-{d:yyyyMMdd}.csv");
    }
}
