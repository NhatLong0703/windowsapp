using Dapper;
using Microsoft.Data.SqlClient;

namespace WebApplication1.Data;

public class Db
{
    private readonly string _cs;

    public Db(IConfiguration cfg)
    {
        _cs = cfg.GetConnectionString("QuanLyKho")
              ?? throw new InvalidOperationException("Thiếu ConnectionStrings:QuanLyKho trong appsettings.json");
    }

    public SqlConnection Open()
    {
        var c = new SqlConnection(_cs);
        c.Open();
        return c;
    }

    // Kho mà nhân viên (thủ kho) đang được quản lý phân công. null = chưa gán kho.
    public async Task<int?> KhoCuaNV(int maNV)
    {
        await using var c = Open();
        return await c.ExecuteScalarAsync<int?>("SELECT MaKho FROM NhanVien WHERE MaNV=@maNV", new { maNV });
    }
}
