using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class HomeController(Db db) : AppController
{
    public async Task<IActionResult> Index(int days = 7)
    {
        if (User.IsBanLe()) return RedirectToAction("Index", "BanLe");
        if (User.IsNhaCungCap()) return RedirectToAction("Index", "NccPortal");

        int? k = null;   // null = Quản lý xem toàn hệ thống
        if (!User.IsAdmin())
        {
            k = await db.KhoCuaNV(User.MaNV());
            if (k == null) return View("~/Views/Shared/ChuaGanKho.cshtml");
        }

        if (days != 14 && days != 30) days = 7;
        await using var c = db.Open();

        var today = DateTime.Today;
        var m0 = new DateTime(today.Year, today.Month, 1);
        var m1 = m0.AddMonths(1);
        var mp = m0.AddMonths(-1);

        async Task<int> Cnt(string tbl, string col, DateTime a, DateTime b) =>
            await c.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {tbl} WHERE {col} >= @a AND {col} < @b AND (@k IS NULL OR MaKho = @k)", new { a, b, k });

        var vm = new DashVM { Days = days };
        if (k != null) vm.KhoTen = await c.ExecuteScalarAsync<string>("SELECT TenKho FROM Kho WHERE MaKho=@k", new { k });

        vm.TongSP = k == null
            ? await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SanPham")
            : await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM ({Sql.Stock}) t WHERE Ton > 0 AND MaKho = @k", new { k });
        vm.GiaTri = await c.ExecuteScalarAsync<decimal>(
            $"SELECT ISNULL(SUM(CAST(Ton AS DECIMAL(18,2)) * DonGia), 0) FROM ({Sql.Stock}) t WHERE Ton > 0 AND (@k IS NULL OR MaKho = @k)", new { k });
        vm.NhapThang = await Cnt("PhieuNhap", "NgayNhap", m0, m1);
        vm.NhapTruoc = await Cnt("PhieuNhap", "NgayNhap", mp, m0);
        vm.XuatThang = await Cnt("PhieuXuat", "NgayXuat", m0, m1);
        vm.XuatTruoc = await Cnt("PhieuXuat", "NgayXuat", mp, m0);
        vm.DonCho = await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM DonDatMua WHERE TrangThai = 'ChoXacNhan' AND (@k IS NULL OR MaKho = @k)", new { k });

        var d0 = today.AddDays(-(days - 1));
        var nhap = (await c.QueryAsync<DaySum>(
            @"SELECT p.NgayNhap AS Ngay, SUM(ct.SoLuong) AS SL FROM PhieuNhap p
              JOIN ChiTietPhieuNhap ct ON ct.MaPN = p.MaPN
              WHERE p.NgayNhap >= @d0 AND (@k IS NULL OR p.MaKho = @k) GROUP BY p.NgayNhap", new { d0, k }))
            .ToDictionary(x => x.Ngay.Date, x => x.SL);
        var xuat = (await c.QueryAsync<DaySum>(
            @"SELECT p.NgayXuat AS Ngay, SUM(ct.SoLuong) AS SL FROM PhieuXuat p
              JOIN ChiTietPhieuXuat ct ON ct.MaPX = p.MaPX
              WHERE p.NgayXuat >= @d0 AND (@k IS NULL OR p.MaKho = @k) GROUP BY p.NgayXuat", new { d0, k }))
            .ToDictionary(x => x.Ngay.Date, x => x.SL);
        for (var i = 0; i < days; i++)
        {
            var d = d0.AddDays(i);
            vm.Chart.Add(new DayPoint { Ngay = d, Nhap = nhap.GetValueOrDefault(d), Xuat = xuat.GetValueOrDefault(d) });
        }

        vm.Low = (await c.QueryAsync<TonRow>(
            $"SELECT TOP 5 * FROM ({Sql.Stock}) t WHERE Ton <= TonToiThieu AND (@k IS NULL OR MaKho = @k) ORDER BY Ton, TenSP", new { k })).ToList();
        vm.LowCount = await c.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM ({Sql.Stock}) t WHERE Ton <= TonToiThieu AND (@k IS NULL OR MaKho = @k)", new { k });
        vm.Recent = (await c.QueryAsync<PhieuRow>(
            $"SELECT TOP 8 * FROM ({Sql.PhieuNhapRows} UNION ALL {Sql.PhieuXuatRows}) u WHERE (@k IS NULL OR u.MaKho = @k) ORDER BY u.Ngay DESC, u.Id DESC", new { k })).ToList();

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
