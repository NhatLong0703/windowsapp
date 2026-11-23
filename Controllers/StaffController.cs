using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebApplication1.Data;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers;

// Nền cho mọi trang nội bộ: chỉ Quản lý và Thủ kho được vào (Nhà bán lẻ bị chặn).
// Với thủ kho, kho được tra lại từ CSDL ở MỌI request nên khi quản lý đổi kho thì có hiệu lực ngay.
[Authorize(Roles = "Admin,ThuKho")]
public abstract class StaffController(Db db) : AppController
{
    protected bool IsAdmin => User.IsAdmin();

    // Kho của thủ kho đang đăng nhập. Luôn null với Quản lý (Quản lý thấy mọi kho).
    protected int? MyKho { get; private set; }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsAdmin)
        {
            MyKho = await db.KhoCuaNV(User.MaNV());
            if (MyKho == null)
            {
                context.Result = View("~/Views/Shared/ChuaGanKho.cshtml");
                return;
            }
        }
        await next();
    }
}
