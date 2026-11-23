using Dapper;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class TonKhoController(Db db) : StaffController(db)
{
    public async Task<IActionResult> Index(int? maKho, string? q, bool thap = false)
    {
        if (!IsAdmin) maKho = MyKho;   // thủ kho chỉ xem kho của mình
        await using var c = db.Open();
        var rows = (await c.QueryAsync<TonRow>(
            $@"SELECT * FROM ({Sql.Stock}) t
               WHERE (@kho IS NULL OR t.MaKho = @kho) AND (@q IS NULL OR t.TenSP LIKE @q)
                 AND (@thap = 0 OR t.Ton <= t.TonToiThieu)
               ORDER BY t.TenKho, t.TenSP", new { kho = maKho, q = Like(q), thap = thap ? 1 : 0 })).ToList();
        ViewBag.Khos = (await c.QueryAsync<Kho>("SELECT * FROM Kho WHERE (@k IS NULL OR MaKho = @k) ORDER BY TenKho", new { k = MyKho })).ToList();
        ViewBag.MaKho = maKho; ViewBag.Q = q; ViewBag.Thap = thap;
        return View(rows);
    }
}
