using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using WebApplication1.Data;

try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

    builder.Services.AddControllersWithViews(o =>
    {
        o.Filters.Add(new AuthorizeFilter());
        o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    });
    builder.Services.AddSingleton<Db>();
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(o =>
        {
            o.LoginPath = "/Account/Login";
            o.AccessDeniedPath = "/Account/Denied";
            o.Cookie.Name = "KhoViet.Auth";
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = true;
        });

    var app = builder.Build();

    DbInit.Run(app.Services.GetRequiredService<Db>());   // nâng cấp CSDL + tạo tài khoản admin lần đầu

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
    }

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapStaticAssets();

    app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    app.Start();

    var uiThread = new Thread(() =>
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "WebApp-error.log"), ex.ToString());
        }
    });
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start();
    uiThread.Join();

    app.StopAsync().Wait();
}
catch (Exception ex)
{
    File.WriteAllText(Path.Combine(Path.GetTempPath(), "WebApp-error.log"), ex.ToString());
    MessageBox.Show(
        ex.Message + "\n\nKiểm tra chuỗi kết nối trong appsettings.json và đảm bảo đã chạy file qlykho2.sql trong SQL Server.",
        "KhoViet - Lỗi khởi động", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
