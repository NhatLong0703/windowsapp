namespace WebApplication1.Models;

public class YeuCauRow
{
    public int MaYC { get; set; }
    public int MaKho { get; set; }
    public string TenKho { get; set; } = "";
    public int MaSP { get; set; }
    public string TenSP { get; set; } = "";
    public string? DonViTinh { get; set; }
    public int TonToiThieu { get; set; }
    public int MaNCC { get; set; }
    public string TenNCC { get; set; } = "";
    public int SoLuong { get; set; }
    public int TonHienTai { get; set; }
    public string? GhiChu { get; set; }
    public string TrangThai { get; set; } = "";
    public string? LyDoTuChoi { get; set; }
    public DateTime NgayTao { get; set; }
    public string NguoiTao { get; set; } = "";
    public DateTime? NgayDuyet { get; set; }
    public DateTime? NgayGiao { get; set; }
    public int? MaPNTao { get; set; }
}

public class SpChonRow
{
    public int MaSP { get; set; }
    public string TenSP { get; set; } = "";
    public string? DonViTinh { get; set; }
    public int TonToiThieu { get; set; }
    public string TenNCC { get; set; } = "";
    public int Ton { get; set; }
}

// Sản phẩm kèm nhà cung cấp, dùng cho form admin gửi yêu cầu nhập tới nhà cung cấp
public class SpNccChon
{
    public int MaSP { get; set; }
    public string TenSP { get; set; } = "";
    public string? DonViTinh { get; set; }
    public int MaNCC { get; set; }
}
