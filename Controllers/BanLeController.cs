using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

// Cổng dành cho nhà bán lẻ: chỉ gửi đơn đặt mua và theo dõi/hủy đơn của chính mình.
[Authorize(Roles = "BanLe")]
public class BanLeController(Db db) : AppController
{
    public async Task<IActionResult> Index()
    {
        await using var c = db.Open();
        var rows = (await c.QueryAsync<DonRow>(
            $"SELECT * FROM ({Sql.DonRows}) d WHERE d.MaBL = @bl ORDER BY d.NgayDat DESC, d.Id DESC", new { bl = User.MaBL() })).ToList();
        return View(rows);
    }

    // Tồn có thể đặt = tồn thực tế - số lượng trong các đơn đang chờ thủ kho xác nhận (tránh hai nhà bán lẻ cùng đặt một số hàng).
    private static readonly string Avail = $@"
SELECT t.MaKho, t.TenKho, t.MaSP, t.TenSP, t.DonViTinh, t.DonGia, t.TonToiThieu, t.Ton - ISNULL(p.SL, 0) AS Ton
FROM ({Sql.Stock}) t
LEFT JOIN (SELECT d.MaKho, c.MaSP, SUM(c.SoLuong) AS SL
           FROM DonDatMua d JOIN ChiTietDonDatMua c ON c.MaDon = d.MaDon
           WHERE d.TrangThai = 'ChoXacNhan' GROUP BY d.MaKho, c.MaSP) p
       ON p.MaKho = t.MaKho AND p.MaSP = t.MaSP";

    private class SpInfo
    {
        public int MaSP { get; set; }
        public string TenSP { get; set; } = "";
        public string? DonViTinh { get; set; }
        public decimal DonGia { get; set; }
    }

    // Nhà bán lẻ chọn sản phẩm trước; hệ thống tự tìm kho đang có hàng. Sản phẩm hết hàng vẫn hiện nhưng không đặt được.
    [HttpGet]
    public async Task<IActionResult> DatMua()
    {
        await using var c = db.Open();
        var avail = (await c.QueryAsync<TonRow>($"SELECT * FROM ({Avail}) a WHERE a.Ton > 0 AND a.DonGia > 0")).ToList();
        var tongBy = avail.GroupBy(r => r.MaSP).ToDictionary(g => g.Key, g => g.Sum(r => r.Ton));
        var all = (await c.QueryAsync<SpInfo>(
            "SELECT MaSP, TenSP, DonViTinh, ISNULL(DonGia, 0) AS DonGia FROM SanPham WHERE ISNULL(DonGia, 0) > 0 ORDER BY TenSP")).ToList();
        var sp = all.ToDictionary(x => x.MaSP.ToString(),
            x => new { ten = x.TenSP, dvt = x.DonViTinh, gia = x.DonGia, tong = tongBy.GetValueOrDefault(x.MaSP) });
        return View(new BanLeDatVM
        {
            Khos = new List<Kho>(),   // không còn chọn kho
            StockJson = "{}",
            SanPhamJson = JsonSerializer.Serialize(sp)
        });
    }

    [HttpPost]
    public async Task<IActionResult> DatMua(string? ghiChu, int[]? maSP, int[]? soLuong)
    {
        maSP ??= Array.Empty<int>(); soLuong ??= Array.Empty<int>();
        var n = Math.Min(maSP.Length, soLuong.Length);
        // gộp các dòng trùng sản phẩm
        var want = Enumerable.Range(0, n).Where(i => maSP[i] > 0 && soLuong[i] > 0)
            .GroupBy(i => maSP[i]).ToDictionary(g => g.Key, g => g.Sum(i => soLuong[i]));
        if (want.Count == 0)
        {
            FlashErr("Hãy chọn ít nhất một sản phẩm với số lượng hợp lệ.");
            return RedirectToAction(nameof(DatMua));
        }

        try
        {
            await using var c = db.Open();
            using var tx = c.BeginTransaction(IsolationLevel.Serializable);   // kiểm tra tồn và tạo đơn trong cùng một giao dịch

            // Giá và tồn lấy từ máy chủ, không tin dữ liệu từ trình duyệt
            var stock = (await c.QueryAsync<TonRow>($"SELECT * FROM ({Avail}) a WHERE a.Ton > 0 AND a.DonGia > 0", null, tx)).ToList();

            // Phân bổ từng sản phẩm vào kho: ưu tiên một kho đủ hàng (ưu tiên kho đã dùng cho dòng khác để ít đơn hơn),
            // nếu không kho nào đủ thì tách số lượng qua nhiều kho, kho nhiều hàng nhất trước.
            var theoKho = new Dictionary<int, List<(int MaSP, int Qty, decimal Gia)>>();
            var daDung = new HashSet<int>();
            foreach (var (id, qty) in want)
            {
                var opts = stock.Where(r => r.MaSP == id).OrderByDescending(r => r.Ton).ToList();
                if (opts.Count == 0)
                {
                    var ten = await c.ExecuteScalarAsync<string?>("SELECT TenSP FROM SanPham WHERE MaSP = @id", new { id }, tx) ?? "Sản phẩm";
                    FlashErr($"\"{ten}\" đã hết hàng ở các kho nên chưa nhận bán. Hãy bỏ sản phẩm này khỏi đơn.");
                    return RedirectToAction(nameof(DatMua));
                }
                var tong = opts.Sum(r => r.Ton);
                if (qty > tong)
                {
                    FlashErr($"\"{opts[0].TenSP}\" chỉ còn tối đa {tong} {opts[0].DonViTinh} có thể đặt. Hãy giảm số lượng.");
                    return RedirectToAction(nameof(DatMua));
                }

                var parts = new List<(TonRow R, int Q)>();
                var one = opts.Where(r => r.Ton >= qty).OrderByDescending(r => daDung.Contains(r.MaKho)).ThenByDescending(r => r.Ton).FirstOrDefault();
                if (one != null) parts.Add((one, qty));
                else
                {
                    var left = qty;
                    foreach (var r in opts)
                    {
                        var q = Math.Min(left, r.Ton);
                        parts.Add((r, q)); left -= q;
                        if (left == 0) break;
                    }
                }
                foreach (var (r, q) in parts)
                {
                    daDung.Add(r.MaKho);
                    if (!theoKho.TryGetValue(r.MaKho, out var l)) theoKho[r.MaKho] = l = new();
                    l.Add((id, q, r.DonGia));
                }
            }

            var gc = string.IsNullOrWhiteSpace(ghiChu) ? null : (ghiChu.Trim().Length > 200 ? ghiChu.Trim()[..200] : ghiChu.Trim());
            var maDons = new List<int>();
            // mỗi kho một đơn, vì thủ kho từng kho xác nhận đơn của kho mình
            foreach (var (maKho, lines) in theoKho)
            {
                var maDon = await c.ExecuteScalarAsync<int>(
                    "INSERT DonDatMua(MaBL, MaKho, GhiChu) VALUES (@bl, @maKho, @gc); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { bl = User.MaBL(), maKho, gc }, tx);
                await c.ExecuteAsync("INSERT ChiTietDonDatMua(MaDon, MaSP, SoLuong, DonGia) VALUES (@maDon, @MaSP, @SoLuong, @DonGia)",
                    lines.Select(l => new { maDon, l.MaSP, SoLuong = l.Qty, DonGia = l.Gia }), tx);
                maDons.Add(maDon);
            }
            tx.Commit();

            if (maDons.Count == 1)
            {
                FlashOk("Đã gửi đơn đặt mua. Vui lòng chờ thủ kho xác nhận.");
                return RedirectToAction(nameof(Detail), new { id = maDons[0] });
            }
            FlashOk($"Hàng nằm ở {maDons.Count} kho nên hệ thống đã tách thành {maDons.Count} đơn, mỗi đơn do thủ kho của kho đó xác nhận.");
            return RedirectToAction(nameof(Index));
        }
        catch (SqlException ex)
        {
            FlashErr(SqlMsg(ex));
            return RedirectToAction(nameof(DatMua));
        }
    }

    public async Task<IActionResult> Detail(int id)
    {
        await using var c = db.Open();
        var don = await c.QueryFirstOrDefaultAsync<DonRow>(
            $"SELECT * FROM ({Sql.DonRows}) d WHERE d.Id = @id AND d.MaBL = @bl", new { id, bl = User.MaBL() });
        if (don == null) return NotFound();
        var lines = (await c.QueryAsync<DonLine>(
            @"SELECT c.MaSP, s.TenSP, s.DonViTinh, c.SoLuong, c.DonGia, 0 AS Ton
              FROM ChiTietDonDatMua c JOIN SanPham s ON s.MaSP = c.MaSP WHERE c.MaDon = @id ORDER BY s.TenSP", new { id })).ToList();
        return View(new DonDetailVM { Don = don, Lines = lines });
    }

    [HttpPost]
    public async Task<IActionResult> Huy(int id)
    {
        await using var c = db.Open();
        var rows = await c.ExecuteAsync(
            "UPDATE DonDatMua SET TrangThai='DaHuy', NgayXuLy=GETDATE() WHERE MaDon=@id AND MaBL=@bl AND TrangThai='ChoXacNhan'",
            new { id, bl = User.MaBL() });
        if (rows == 0) FlashErr("Không thể hủy: đơn đã được thủ kho xử lý.");
        else FlashOk("Đã hủy đơn.");
        return RedirectToAction(nameof(Detail), new { id });
    }
}
