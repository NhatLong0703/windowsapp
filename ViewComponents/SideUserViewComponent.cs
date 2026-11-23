using Dapper;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.ViewComponents;

public class SideUserViewComponent(Db db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var u = UserClaimsPrincipal;
        var vm = new SideUserVM { Name = u.Identity?.Name ?? "", RoleLabel = Roles.Label(u.VaiTro()) };
        if (u.IsThuKho())
        {
            await using var c = db.Open();
            vm.KhoTen = await c.ExecuteScalarAsync<string?>(
                "SELECT k.TenKho FROM NhanVien n JOIN Kho k ON k.MaKho = n.MaKho WHERE n.MaNV=@m", new { m = u.MaNV() });
        }
        return View(vm);
    }
}
