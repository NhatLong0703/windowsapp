using Dapper;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;

namespace WebApplication1.ViewComponents;

public class LowStockBellViewComponent(Db db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        int? k = null;
        if (!UserClaimsPrincipal.IsAdmin())
        {
            k = await db.KhoCuaNV(UserClaimsPrincipal.MaNV());
            if (k == null) return View(0);
        }
        await using var c = db.Open();
        var n = await c.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM ({Sql.Stock}) t WHERE Ton <= TonToiThieu AND (@k IS NULL OR MaKho = @k)", new { k });
        return View(n);
    }
}
