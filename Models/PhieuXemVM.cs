namespace WebApplication1.Models;

public class XemDong
{
    public string TenSP { get; set; } = "";
    public string? DonViTinh { get; set; }
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
}

public class PhieuXemVM
{
    public PhieuRow Phieu { get; set; } = new();
    public List<XemDong> Dong { get; set; } = new();
    public string? DonLienKet { get; set; }   // mã đơn bán lẻ nếu phiếu xuất sinh ra từ đơn
}
