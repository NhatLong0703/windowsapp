using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace WebApplication1.Controllers;

public abstract class AppController : Controller
{
    protected void FlashOk(string m) => TempData["Ok"] = m;
    protected void FlashErr(string m) => TempData["Err"] = m;

    protected static string? Like(string? s) => string.IsNullOrWhiteSpace(s) ? null : "%" + s.Trim() + "%";

    protected bool Valid()
    {
        if (ModelState.IsValid) return true;
        FlashErr("Dữ liệu chưa hợp lệ: " + string.Join("; ",
            ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrEmpty(m))));
        return false;
    }

    protected static string SqlMsg(SqlException ex) => ex.Number switch
    {
        547 => "Không thực hiện được vì dữ liệu này đang được dùng ở nơi khác (ví dụ đã có phiếu liên quan).",
        2627 or 2601 => "Dữ liệu bị trùng (ví dụ tên đã tồn tại).",
        _ => "Lỗi cơ sở dữ liệu: " + ex.Message
    };

    // act trả về null nếu thành công, hoặc chuỗi lỗi
    protected async Task Try(Func<Task<string?>> act, string okMsg)
    {
        try
        {
            var err = await act();
            if (err == null) FlashOk(okMsg); else FlashErr(err);
        }
        catch (SqlException ex) { FlashErr(SqlMsg(ex)); }
    }

    protected async Task Try(Func<Task> act, string okMsg)
    {
        try { await act(); FlashOk(okMsg); }
        catch (SqlException ex) { FlashErr(SqlMsg(ex)); }
    }
}
