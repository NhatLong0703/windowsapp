using Dapper;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;

namespace WebApplication1.ViewComponents;

// Số việc đang chờ xử lý cạnh mục menu.
//   loai = "dexuat"  : admin  -> số đề xuất sản phẩm chờ duyệt
//   loai = "yeucau"  : admin  -> số yêu cầu nhập chờ duyệt; nhà cung cấp -> số yêu cầu nhập chờ giao
public class DeXuatBadgeViewComponent(Db db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string loai = "dexuat")
    {
        var admin = UserClaimsPrincipal.IsInRole("Admin");
        var ncc = UserClaimsPrincipal.IsInRole("NhaCungCap");
        int n;
        await using var c = db.Open();
        if (loai == "yeucau" && admin)
            n = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM YeuCauNhap WHERE TrangThai = N'ChoAdmin'");
        else if (loai == "yeucau" && ncc)
            n = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM YeuCauNhap WHERE TrangThai = N'ChoNCC' AND MaNCC = @m", new { m = UserClaimsPrincipal.MaNCC() });
        else if (loai == "dexuat" && admin)
            n = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DeXuatSanPham WHERE TrangThai = N'ChoDuyet'");
        else
            return Content("");
        return View(n);
    }
}
