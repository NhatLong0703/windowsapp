using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class LoaiHang
{
    public int MaLoai { get; set; }
    [Required(ErrorMessage = "Nhập tên loại hàng")] public string TenLoai { get; set; } = "";
    public int SoSP { get; set; }
}

public class Kho
{
    public int MaKho { get; set; }
    [Required(ErrorMessage = "Nhập tên kho")] public string TenKho { get; set; } = "";
    public string? DiaChi { get; set; }
    public string? SoDienThoai { get; set; }
    public string? ThuKho { get; set; }
}

public class SanPham
{
    public int MaSP { get; set; }
    [Required(ErrorMessage = "Nhập tên sản phẩm")] public string TenSP { get; set; } = "";
    public string? DonViTinh { get; set; }
    public decimal? DonGia { get; set; }
    public string? MoTa { get; set; }
    public int? MaLoai { get; set; }
    public int TonToiThieu { get; set; }
    public string? TenLoai { get; set; }
    public int TongTon { get; set; }          // tổng tồn (Quản lý: mọi kho; Thủ kho: kho của mình)
    public string? TonTheoKho { get; set; }   // vd: "Kho 1: 20 · Kho 2: 5"
}

public class NhaCungCap
{
    public int MaNCC { get; set; }
    [Required(ErrorMessage = "Nhập tên đối tác")] public string TenNCC { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public string? Email { get; set; }
    public int SoDaiDien { get; set; }
    public string? TenLoaiHang { get; set; }
}

public class NguoiDaiDien
{
    public int MaDD { get; set; }
    public int MaNCC { get; set; }
    [Required(ErrorMessage = "Nhập họ tên người đại diện")] public string HoTen { get; set; } = "";
    public string? ChucVu { get; set; }
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
}

public class NhanVien
{
    public int MaNV { get; set; }
    [Required(ErrorMessage = "Nhập họ tên nhân viên")] public string HoTen { get; set; } = "";
    public string? GioiTinh { get; set; }
    public DateTime? NgaySinh { get; set; }
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public string? ChucVu { get; set; }
    public string? TenDangNhap { get; set; }
    public string? VaiTro { get; set; }
    public string? MatKhau { get; set; }
    public int? MaKho { get; set; }
    public string? TenKho { get; set; }
}

public class TaiKhoanRow
{
    public int MaTK { get; set; }
    public string MatKhauHash { get; set; } = "";
    public string VaiTro { get; set; } = "";
    public int MaNV { get; set; }
    public int MaBL { get; set; }
    public string HoTen { get; set; } = "";
}

public class PhieuRow
{
    public string Loai { get; set; } = "";
    public int Id { get; set; }
    public string Ma { get; set; } = "";
    public DateTime Ngay { get; set; }
    public int MaKho { get; set; }
    public string TenKho { get; set; } = "";
    public string? DoiTac { get; set; }
    public string NguoiTao { get; set; } = "";
    public string? GhiChu { get; set; }
    public decimal GiaTri { get; set; }
}

public class PhieuHead
{
    public int Id { get; set; }
    public DateTime Ngay { get; set; } = DateTime.Today;
    public int MaKho { get; set; }
    public int? MaNCC { get; set; }
    public string? GhiChu { get; set; }
}

public class PhieuLine
{
    public int MaSP { get; set; }
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
}

public class PhieuFormVM
{
    public string Loai { get; set; } = "nhap";
    public PhieuHead Head { get; set; } = new();
    public List<PhieuLine> Lines { get; set; } = new();
    public List<Kho> Khos { get; set; } = new();
    public List<NhaCungCap> Nccs { get; set; } = new();
    public List<SanPham> Sps { get; set; } = new();
    public bool KhoCoDinh { get; set; }
    public bool GiaKhoa { get; set; }
}

// Kho kèm số liệu tồn kho tổng hợp (dùng cho trang Kho hàng)
public class KhoRow : Kho
{
    public int SoMatHang { get; set; }   // số mặt hàng còn hàng (tồn > 0)
    public int TongTon { get; set; }     // tổng số lượng tồn
    public decimal GiaTri { get; set; }  // giá trị tồn theo đơn giá danh mục
    public int SapHet { get; set; }      // số mặt hàng sắp hết hoặc hết
}

public class TonRow
{
    public int MaKho { get; set; }
    public string TenKho { get; set; } = "";
    public int MaSP { get; set; }
    public string TenSP { get; set; } = "";
    public string? DonViTinh { get; set; }
    public decimal DonGia { get; set; }
    public int TonToiThieu { get; set; }
    public int Ton { get; set; }
}

public class DaySum { public DateTime Ngay { get; set; } public int SL { get; set; } }
public class DayPoint { public DateTime Ngay { get; set; } public int Nhap { get; set; } public int Xuat { get; set; } }

public class DashVM
{
    public int Days { get; set; } = 7;
    public int TongSP { get; set; }
    public decimal GiaTri { get; set; }
    public int NhapThang { get; set; }
    public int NhapTruoc { get; set; }
    public int XuatThang { get; set; }
    public int XuatTruoc { get; set; }
    public List<DayPoint> Chart { get; set; } = new();
    public List<TonRow> Low { get; set; } = new();
    public int LowCount { get; set; }
    public List<PhieuRow> Recent { get; set; } = new();
    public string? KhoTen { get; set; }
    public int DonCho { get; set; }
}

public class NccDetailVM
{
    public NhaCungCap Ncc { get; set; } = new();
    public List<NguoiDaiDien> Reps { get; set; } = new();
    public List<LoaiHang> AllLoai { get; set; } = new();
    public HashSet<int> Chosen { get; set; } = new();
}

public class BaoCaoRow
{
    public int MaSP { get; set; }
    public string TenSP { get; set; } = "";
    public string? DonViTinh { get; set; }
    public int NhapSL { get; set; }
    public decimal NhapGT { get; set; }
    public int XuatSL { get; set; }
    public decimal XuatGT { get; set; }
}

public class SpGia { public int MaSP { get; set; } public decimal Gia { get; set; } }

public class NhaBanLe
{
    public int MaBL { get; set; }
    [Required(ErrorMessage = "Nhập tên nhà bán lẻ")] public string TenBL { get; set; } = "";
    public string? NguoiLienHe { get; set; }
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public string? Email { get; set; }
    public string? TenDangNhap { get; set; }
    public string? MatKhau { get; set; }
}

public class DonRow
{
    public int Id { get; set; }
    public string Ma { get; set; } = "";
    public DateTime NgayDat { get; set; }
    public int MaBL { get; set; }
    public string TenBL { get; set; } = "";
    public int MaKho { get; set; }
    public string TenKho { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public string? GhiChu { get; set; }
    public string? LyDoTuChoi { get; set; }
    public string? NguoiXuLy { get; set; }
    public DateTime? NgayXuLy { get; set; }
    public int? MaPX { get; set; }
    public int SoDong { get; set; }
    public decimal GiaTri { get; set; }
}

public class DonLine
{
    public int MaSP { get; set; }
    public string TenSP { get; set; } = "";
    public string? DonViTinh { get; set; }
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public int Ton { get; set; }
}

public class DonDetailVM
{
    public DonRow Don { get; set; } = new();
    public List<DonLine> Lines { get; set; } = new();
}

public class DonHead
{
    public int MaDon { get; set; }
    public int MaKho { get; set; }
    public string TrangThai { get; set; } = "";
    public string Ma { get; set; } = "";
    public string TenBL { get; set; } = "";
}

public class BanLeDatVM
{
    public List<Kho> Khos { get; set; } = new();
    public string StockJson { get; set; } = "{}";
    public string SanPhamJson { get; set; } = "{}";
}

public class SideUserVM
{
    public string Name { get; set; } = "";
    public string RoleLabel { get; set; } = "";
    public string? KhoTen { get; set; }
}
