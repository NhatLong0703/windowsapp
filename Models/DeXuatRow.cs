namespace WebApplication1.Models;

public class DeXuatRow
{
    public int MaDX { get; set; }
    public int MaNCC { get; set; }
    public string TenNCC { get; set; } = "";
    public string TenSP { get; set; } = "";
    public string? DonViTinh { get; set; }
    public decimal DonGia { get; set; }
    public string? MoTa { get; set; }
    public int? MaLoai { get; set; }
    public string? TenLoai { get; set; }
    public int SoLuong { get; set; }
    public int? MaKho { get; set; }
    public string? TenKho { get; set; }
    public string TrangThai { get; set; } = "";
    public string? LyDoTuChoi { get; set; }
    public DateTime NgayTao { get; set; }
    public DateTime? NgayDuyet { get; set; }
    public int? MaSPTao { get; set; }
    public int? MaPNTao { get; set; }
}
