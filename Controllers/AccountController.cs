using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class AccountController(Db db) : AppController
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(string? username, string? password, string? returnUrl)
    {
        await using var c = db.Open();
        var tk = await c.QueryFirstOrDefaultAsync<TaiKhoanRow>(
            @"SELECT t.MaTK, t.MatKhauHash, t.VaiTro, ISNULL(t.MaNV, 0) AS MaNV, ISNULL(t.MaBL, 0) AS MaBL,
                     COALESCE(n.HoTen, b.TenBL, ncc.TenNCC, t.TenDangNhap) AS HoTen
              FROM TaiKhoan t
              LEFT JOIN NhanVien n ON n.MaNV = t.MaNV
              LEFT JOIN NhaBanLe b ON b.MaBL = t.MaBL
              LEFT JOIN NhaCungCap ncc ON ncc.MaNCC = t.MaNCC
              WHERE t.TenDangNhap = @u AND t.HoatDong = 1", new { u = username?.Trim() });

        if (tk == null || string.IsNullOrEmpty(password) || !Pw.Verify(password, tk.MatKhauHash))
        {
            ViewBag.Err = "Sai tên đăng nhập hoặc mật khẩu.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // Mã nhà cung cấp (0 nếu tài khoản không phải của nhà cung cấp)
        var maNCC = await c.ExecuteScalarAsync<int?>("SELECT MaNCC FROM TaiKhoan WHERE MaTK=@MaTK", new { tk.MaTK }) ?? 0;

        var id = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, tk.MaTK.ToString()),
            new Claim(ClaimTypes.Name, tk.HoTen),
            new Claim(ClaimTypes.Role, tk.VaiTro),
            new Claim("MaNV", tk.MaNV.ToString()),
            new Claim("MaBL", tk.MaBL.ToString()),
            new Claim("MaNCC", maNCC.ToString())
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult Denied() => View();

    [HttpGet]
    public IActionResult DoiMatKhau() => View();

    [HttpPost]
    public async Task<IActionResult> DoiMatKhau(string? matCu, string? matMoi, string? nhapLai)
    {
        if (string.IsNullOrEmpty(matMoi) || matMoi.Length < 6) { ViewBag.Err = "Mật khẩu mới cần ít nhất 6 ký tự."; return View(); }
        if (matMoi != nhapLai) { ViewBag.Err = "Hai lần nhập mật khẩu mới không khớp."; return View(); }

        var maTK = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var c = db.Open();
        var hash = await c.ExecuteScalarAsync<string>("SELECT MatKhauHash FROM TaiKhoan WHERE MaTK=@maTK", new { maTK });
        if (hash == null || string.IsNullOrEmpty(matCu) || !Pw.Verify(matCu, hash)) { ViewBag.Err = "Mật khẩu hiện tại không đúng."; return View(); }

        await c.ExecuteAsync("UPDATE TaiKhoan SET MatKhauHash=@h WHERE MaTK=@maTK", new { h = Pw.Hash(matMoi), maTK });
        FlashOk("Đã đổi mật khẩu.");
        return RedirectToAction("Index", "Home");
    }
}