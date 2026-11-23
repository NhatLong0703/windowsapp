using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Html;

namespace WebApplication1.Helpers;

public static class Roles
{
    public const string Admin = "Admin";      // Quản lý
    public const string ThuKho = "ThuKho";    // Thủ kho (gắn với 1 kho)
    public const string BanLe = "BanLe";      // Nhà bán lẻ
    public const string NhaCungCap = "NhaCungCap";   // Nhà cung cấp (xem phiếu nhập và sản phẩm của mình)

    public static string Label(string? r) => r switch
    {
        Admin => "Quản lý",
        ThuKho => "Thủ kho",
        BanLe => "Nhà bán lẻ",
        NhaCungCap => "Nhà cung cấp",
        _ => ""
    };
}

public static class UserExt
{
    public static int MaNV(this ClaimsPrincipal u) => int.Parse(u.FindFirstValue("MaNV") ?? "0");
    public static int MaBL(this ClaimsPrincipal u) => int.Parse(u.FindFirstValue("MaBL") ?? "0");
    public static int MaNCC(this ClaimsPrincipal u) => int.Parse(u.FindFirstValue("MaNCC") ?? "0");
    public static string VaiTro(this ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.Role) ?? "";
    public static bool IsAdmin(this ClaimsPrincipal u) => u.IsInRole(Roles.Admin);
    public static bool IsThuKho(this ClaimsPrincipal u) => u.IsInRole(Roles.ThuKho);
    public static bool IsBanLe(this ClaimsPrincipal u) => u.IsInRole(Roles.BanLe);
    public static bool IsNhaCungCap(this ClaimsPrincipal u) => u.IsInRole(Roles.NhaCungCap);
}

public static class Fmt
{
    private static readonly CultureInfo Vi = new("vi-VN");
    private static readonly string[] Th = { "Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" };

    public static string Num(decimal d) => d.ToString("N0", Vi);
    public static string Money(decimal d) => d.ToString("N0", Vi) + " đ";

    public static string Short(decimal d)
    {
        var a = Math.Abs(d);
        if (a >= 1_000_000_000m) return (d / 1_000_000_000m).ToString("0.##", Vi) + " tỷ";
        if (a >= 1_000_000m) return (d / 1_000_000m).ToString("0.##", Vi) + " triệu";
        return d.ToString("N0", Vi);
    }

    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string DateInput(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string LongDate(DateTime d) => $"{Th[(int)d.DayOfWeek]}, {d.Day:00} tháng {d.Month:00}, {d.Year}";
    public static string MaSP(int id) => "SP-" + id.ToString("00000");

    public static (string Text, string Css) Don(string tt) => tt switch
    {
        "ChoXacNhan" => ("Chờ xác nhận", "warn"),
        "DaXacNhan" => ("Đã xác nhận", "ok"),
        "TuChoi" => ("Bị từ chối", "bad"),
        "DaHuy" => ("Đã hủy", "gray"),
        _ => (tt, "gray")
    };
}

public static class Ui
{
    public static string Initials(string? n)
    {
        var p = (n ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length == 0) return "?";
        if (p.Length == 1) return p[0][..1].ToUpperInvariant();
        return (p[^2][..1] + p[^1][..1]).ToUpperInvariant();
    }

    public static IHtmlContent Icon(string name)
    {
        var p = name switch
        {
            "home" => "<path d='M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z'/><polyline points='9 22 9 12 15 12 15 22'/>",
            "grid" => "<rect x='3' y='3' width='7' height='7'/><rect x='14' y='3' width='7' height='7'/><rect x='14' y='14' width='7' height='7'/><rect x='3' y='14' width='7' height='7'/>",
            "box" => "<path d='M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z'/><polyline points='3.27 6.96 12 12.01 20.73 6.96'/><line x1='12' y1='22.08' x2='12' y2='12'/>",
            "warehouse" => "<path d='M3 21V8l9-5 9 5v13'/><path d='M7 21v-8h10v8'/>",
            "truck" => "<rect x='1' y='3' width='15' height='13'/><polygon points='16 8 20 8 23 11 23 16 16 16 16 8'/><circle cx='5.5' cy='18.5' r='2.5'/><circle cx='18.5' cy='18.5' r='2.5'/>",
            "user" => "<path d='M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2'/><circle cx='12' cy='7' r='4'/>",
            "in" => "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/><polyline points='7 10 12 15 17 10'/><line x1='12' y1='15' x2='12' y2='3'/>",
            "out" => "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/><polyline points='17 8 12 3 7 8'/><line x1='12' y1='3' x2='12' y2='15'/>",
            "report" => "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><polyline points='14 2 14 8 20 8'/><line x1='8' y1='13' x2='16' y2='13'/><line x1='8' y1='17' x2='16' y2='17'/>",
            "tag" => "<path d='M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z'/><line x1='7' y1='7' x2='7.01' y2='7'/>",
            "bell" => "<path d='M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9'/><path d='M13.73 21a2 2 0 0 1-3.46 0'/>",
            "plus" => "<line x1='12' y1='5' x2='12' y2='19'/><line x1='5' y1='12' x2='19' y2='12'/>",
            "edit" => "<path d='M12 20h9'/><path d='M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4z'/>",
            "trash" => "<polyline points='3 6 5 6 21 6'/><path d='M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6'/><path d='M10 11v6M14 11v6'/>",
            "download" => "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/><polyline points='7 10 12 15 17 10'/><line x1='12' y1='15' x2='12' y2='3'/>",
            _ => ""
        };
        return new HtmlString($"<svg class=\"ic\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{p}</svg>");
    }
}