using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Helpers;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

// Phiếu nhập (loai = "nhap") và phiếu xuất (loai = "xuat").
// Thủ kho: chỉ làm việc với kho của mình; phiếu xuất luôn lấy đơn giá theo danh mục sản phẩm (không tự đổi giá).
public class PhieuController(Db db) : StaffController(db)
{
    private static bool OkLoai(string? loai) => loai is "nhap" or "xuat";

    // Thủ kho không tạo/sửa/xóa phiếu trực tiếp:
    //  - phiếu xuất: chỉ xem; sinh ra khi xác nhận đơn bán lẻ (DonBanLeController.XacNhan)
    //  - phiếu nhập: không ai tạo tay; hàng chỉ vào kho qua "Yêu cầu nhập hàng" (YeuCauNhapController) khi nhà cung cấp xác nhận giao.
    //    Danh sách phiếu nhập chỉ còn để xem lịch sử.
    private bool CamSua(string loai) => loai == "nhap" || !IsAdmin;
    private static readonly string NegSql = $"SELECT COUNT(*) FROM ({Sql.Stock}) t WHERE Ton < 0";

    public async Task<IActionResult> Index(string loai, string? q, DateTime? tu, DateTime? den, int? maKho)
    {
        if (!OkLoai(loai)) return NotFound();
        if (loai == "nhap" && !IsAdmin) return RedirectToAction("Index", "YeuCauNhap");
        if (!IsAdmin) maKho = MyKho;
        ViewBag.CoTheSua = !CamSua(loai);   // View dùng cờ này để ẩn nút tạo/sửa/xóa phiếu xuất với thủ kho
        var inner = loai == "nhap" ? Sql.PhieuNhapRows : Sql.PhieuXuatRows;
        await using var c = db.Open();
        var rows = (await c.QueryAsync<PhieuRow>(
            $@"SELECT * FROM ({inner}) r
               WHERE (@q IS NULL OR r.Ma LIKE @q OR r.TenKho LIKE @q OR r.DoiTac LIKE @q OR r.NguoiTao LIKE @q OR r.GhiChu LIKE @q)
                 AND (@tu IS NULL OR r.Ngay >= @tu) AND (@den IS NULL OR r.Ngay <= @den)
                 AND (@maKho IS NULL OR r.MaKho = @maKho)
               ORDER BY r.Ngay DESC, r.Id DESC", new { q = Like(q), tu, den, maKho })).ToList();
        ViewBag.Loai = loai;
        ViewBag.Khos = (await c.QueryAsync<Kho>("SELECT * FROM Kho WHERE (@k IS NULL OR MaKho = @k) ORDER BY TenKho", new { k = MyKho })).ToList();
        ViewBag.Q = q; ViewBag.MaKho = maKho;
        ViewBag.Tu = tu?.ToString("yyyy-MM-dd"); ViewBag.Den = den?.ToString("yyyy-MM-dd");
        return View(rows);
    }

    private class SpNccRow
    {
        public int MaSP { get; set; }
        public int? MaNCC { get; set; }
    }

    private async Task<PhieuFormVM> BuildVm(SqlConnection c, string loai, PhieuHead head, List<PhieuLine> lines)
    {
        // ViewBag.SpNcc: MaSP -> MaNCC, để View chỉ hiện sản phẩm thuộc nhà cung cấp đã chọn
        ViewBag.SpNcc = (await c.QueryAsync<SpNccRow>("SELECT MaSP, MaNCC FROM SanPham")).ToDictionary(x => x.MaSP, x => x.MaNCC);
        return new PhieuFormVM
        {
            Loai = loai, Head = head, Lines = lines,
            KhoCoDinh = !IsAdmin,
            GiaKhoa = !IsAdmin && loai == "xuat",
            Khos = (await c.QueryAsync<Kho>("SELECT * FROM Kho WHERE (@k IS NULL OR MaKho = @k) ORDER BY TenKho", new { k = MyKho })).ToList(),
            Nccs = (await c.QueryAsync<NhaCungCap>("SELECT MaNCC, TenNCC FROM NhaCungCap ORDER BY TenNCC")).ToList(),
            Sps = (await c.QueryAsync<SanPham>("SELECT MaSP, TenSP, DonViTinh, DonGia FROM SanPham ORDER BY TenSP")).ToList()
        };
    }

    // Thủ kho chỉ được đụng vào phiếu thuộc kho của mình
    private async Task<bool> ThuocKho(SqlConnection c, string loai, int id)
    {
        if (IsAdmin || id == 0) return true;
        var sql = loai == "nhap" ? "SELECT MaKho FROM PhieuNhap WHERE MaPN=@id" : "SELECT MaKho FROM PhieuXuat WHERE MaPX=@id";
        return await c.ExecuteScalarAsync<int?>(sql, new { id }) == MyKho;
    }

    // Phiếu xuất sinh ra từ đơn của nhà bán lẻ thì không sửa/xóa trực tiếp
    private static async Task<string?> DonLienKet(SqlConnection c, string loai, int id)
    {
        if (loai != "xuat" || id == 0) return null;
        return await c.ExecuteScalarAsync<string?>(
            "SELECT 'DM-' + CAST(YEAR(NgayDat) AS VARCHAR(4)) + '-' + RIGHT('0000' + CAST(MaDon AS VARCHAR(10)), 4) FROM DonDatMua WHERE MaPX=@id", new { id });
    }

    // Xem chi tiết phiếu (chỉ đọc). Thủ kho chỉ xem được phiếu của kho mình.
    public async Task<IActionResult> Detail(string loai, int id)
    {
        if (!OkLoai(loai)) return NotFound();
        var nhap = loai == "nhap";
        await using var c = db.Open();
        var phieu = await c.QueryFirstOrDefaultAsync<PhieuRow>(
            $"SELECT * FROM ({(nhap ? Sql.PhieuNhapRows : Sql.PhieuXuatRows)}) r WHERE r.Id = @id AND (@k IS NULL OR r.MaKho = @k)", new { id, k = MyKho });
        if (phieu == null) return NotFound();
        var dong = (await c.QueryAsync<XemDong>(nhap
            ? @"SELECT s.TenSP, s.DonViTinh, c.SoLuong, c.DonGiaNhap AS DonGia
                FROM ChiTietPhieuNhap c JOIN SanPham s ON s.MaSP = c.MaSP WHERE c.MaPN = @id ORDER BY c.MaCTPN"
            : @"SELECT s.TenSP, s.DonViTinh, c.SoLuong, c.DonGiaXuat AS DonGia
                FROM ChiTietPhieuXuat c JOIN SanPham s ON s.MaSP = c.MaSP WHERE c.MaPX = @id ORDER BY c.MaCTPX", new { id })).ToList();
        ViewBag.Loai = loai;
        return View(new PhieuXemVM { Phieu = phieu, Dong = dong, DonLienKet = await DonLienKet(c, loai, id) });
    }

    public async Task<IActionResult> Form(string loai, int id = 0, int? maKho = null, int? maSP = null)
    {
        if (!OkLoai(loai)) return NotFound();
        if (CamSua(loai))
        {
            if (loai == "nhap")
            {
                if (id > 0 && IsAdmin) return RedirectToAction(nameof(Detail), new { loai, id });
                // Nút "Nhập kho" ở trang Sản phẩm/Kho chuyển thẳng sang form yêu cầu nhập (điền sẵn kho, sản phẩm)
                return RedirectToAction("Index", "YeuCauNhap", new { maSP, maKho });
            }
            if (id > 0) return RedirectToAction(nameof(Detail), new { loai, id });
            FlashErr("Thủ kho chỉ được xem phiếu xuất. Phiếu xuất được tạo khi bạn xác nhận đơn bán lẻ.");
            return RedirectToAction(nameof(Index), new { loai });
        }
        var nhap = loai == "nhap";
        await using var c = db.Open();
        if (!await ThuocKho(c, loai, id)) return NotFound();

        var head = new PhieuHead { MaKho = MyKho ?? 0 };
        var lines = new List<PhieuLine>();
        if (id > 0)
        {
            var h = nhap
                ? await c.QueryFirstOrDefaultAsync<PhieuHead>("SELECT MaPN AS Id, NgayNhap AS Ngay, MaKho, MaNCC, GhiChu FROM PhieuNhap WHERE MaPN=@id", new { id })
                : await c.QueryFirstOrDefaultAsync<PhieuHead>("SELECT MaPX AS Id, NgayXuat AS Ngay, MaKho, CAST(NULL AS INT) AS MaNCC, GhiChu FROM PhieuXuat WHERE MaPX=@id", new { id });
            if (h == null) return NotFound();
            head = h;
            lines = (await c.QueryAsync<PhieuLine>(nhap
                ? "SELECT MaSP, SoLuong, DonGiaNhap AS DonGia FROM ChiTietPhieuNhap WHERE MaPN=@id ORDER BY MaCTPN"
                : "SELECT MaSP, SoLuong, DonGiaXuat AS DonGia FROM ChiTietPhieuXuat WHERE MaPX=@id ORDER BY MaCTPX", new { id })).ToList();
        }
        else
        {
            // Mở từ trang Kho / Sản phẩm: điền sẵn kho (chỉ Quản lý chọn được kho) và dòng hàng đầu tiên
            if (IsAdmin && maKho is > 0 && await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Kho WHERE MaKho=@maKho", new { maKho }) > 0)
                head.MaKho = maKho.Value;
            if (maSP is > 0)
            {
                var sp = await c.QueryFirstOrDefaultAsync<SpGia>("SELECT MaSP, ISNULL(DonGia, 0) AS Gia FROM SanPham WHERE MaSP=@maSP", new { maSP });
                if (sp != null) lines.Add(new PhieuLine { MaSP = sp.MaSP, SoLuong = 1, DonGia = sp.Gia });
                if (sp != null && nhap)
                    head.MaNCC = await c.ExecuteScalarAsync<int?>("SELECT MaNCC FROM SanPham WHERE MaSP=@maSP", new { maSP });
            }
        }
        return View(await BuildVm(c, loai, head, lines));
    }

    [HttpPost]
    public async Task<IActionResult> Save(string loai, int id, DateTime ngay, int maKho, int? maNCC, string? ghiChu,
        int[]? maSP, int[]? soLuong, decimal[]? donGia)
    {
        if (!OkLoai(loai)) return NotFound();
        if (CamSua(loai)) return Forbid();
        var nhap = loai == "nhap";
        if (!IsAdmin) maKho = MyKho!.Value;   // bỏ qua kho do trình duyệt gửi lên

        maSP ??= Array.Empty<int>(); soLuong ??= Array.Empty<int>(); donGia ??= Array.Empty<decimal>();
        var n = Math.Min(maSP.Length, Math.Min(soLuong.Length, donGia.Length));
        var lines = Enumerable.Range(0, n).Select(i => new PhieuLine { MaSP = maSP[i], SoLuong = soLuong[i], DonGia = donGia[i] }).ToList();
        var head = new PhieuHead { Id = id, Ngay = ngay, MaKho = maKho, MaNCC = nhap ? maNCC : null, GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim() };

        await using var c = db.Open();
        if (!await ThuocKho(c, loai, id)) return Forbid();

        async Task<IActionResult> Back(string err)
        {
            FlashErr(err);
            return View("Form", await BuildVm(c, loai, head, lines));
        }

        if (lines.Count == 0) return await Back("Phiếu cần ít nhất một dòng hàng.");
        if (lines.Any(l => l.MaSP <= 0 || l.SoLuong <= 0 || l.DonGia < 0)) return await Back("Mỗi dòng cần chọn sản phẩm, số lượng > 0 và đơn giá ≥ 0.");
        if (maKho <= 0) return await Back("Hãy chọn kho.");
        if (nhap && (maNCC == null || maNCC <= 0)) return await Back("Hãy chọn nhà cung cấp.");
        if (nhap)
        {
            var ids = lines.Select(l => l.MaSP).Distinct().ToArray();
            var sai = (await c.QueryAsync<string>(
                "SELECT TenSP FROM SanPham WHERE MaSP IN @ids AND (MaNCC IS NULL OR MaNCC <> @maNCC)", new { ids, maNCC })).ToList();
            if (sai.Count > 0)
                return await Back("Sản phẩm không thuộc nhà cung cấp đã chọn: " + string.Join(", ", sai));
        }
        if (await DonLienKet(c, loai, id) is string ma)
            return await Back($"Phiếu xuất này được tạo từ đơn bán lẻ {ma}, không thể sửa trực tiếp.");

        // Thủ kho không được đổi giá xuất: dùng giá cũ của dòng đó (khi sửa phiếu), hoặc giá hiện tại trong danh mục
        if (!IsAdmin && !nhap)
        {
            var cur = new Dictionary<int, decimal>();
            foreach (var x in await c.QueryAsync<SpGia>("SELECT MaSP, ISNULL(DonGia, 0) AS Gia FROM SanPham")) cur[x.MaSP] = x.Gia;
            var old = new Dictionary<int, decimal>();
            if (id > 0)
                foreach (var x in await c.QueryAsync<SpGia>("SELECT MaSP, DonGiaXuat AS Gia FROM ChiTietPhieuXuat WHERE MaPX=@id", new { id })) old[x.MaSP] = x.Gia;
            foreach (var l in lines) l.DonGia = old.TryGetValue(l.MaSP, out var g) ? g : cur.GetValueOrDefault(l.MaSP);
        }

        string H = nhap ? "PhieuNhap" : "PhieuXuat", D = nhap ? "ChiTietPhieuNhap" : "ChiTietPhieuXuat";
        string pk = nhap ? "MaPN" : "MaPX", dateCol = nhap ? "NgayNhap" : "NgayXuat", priceCol = nhap ? "DonGiaNhap" : "DonGiaXuat";

        try
        {
            using var tx = c.BeginTransaction();
            var before = await c.ExecuteScalarAsync<int>(NegSql, null, tx);

            var pid = id;
            if (id == 0)
            {
                var sqlIns = nhap
                    ? "INSERT PhieuNhap(MaNV, MaKho, MaNCC, NgayNhap, GhiChu) VALUES (@MaNV, @MaKho, @MaNCC, @Ngay, @GhiChu); SELECT CAST(SCOPE_IDENTITY() AS INT);"
                    : "INSERT PhieuXuat(MaNV, MaKho, NgayXuat, GhiChu) VALUES (@MaNV, @MaKho, @Ngay, @GhiChu); SELECT CAST(SCOPE_IDENTITY() AS INT);";
                pid = await c.ExecuteScalarAsync<int>(sqlIns, new { MaNV = User.MaNV(), head.MaKho, head.MaNCC, head.Ngay, head.GhiChu }, tx);
            }
            else
            {
                var sqlUpd = $"UPDATE {H} SET MaKho=@MaKho, {dateCol}=@Ngay, GhiChu=@GhiChu" + (nhap ? ", MaNCC=@MaNCC" : "") + $" WHERE {pk}=@id";
                var rows = await c.ExecuteAsync(sqlUpd, new { head.MaKho, head.MaNCC, head.Ngay, head.GhiChu, id = pid }, tx);
                if (rows == 0) { tx.Rollback(); return NotFound(); }
                await c.ExecuteAsync($"DELETE {D} WHERE {pk}=@id", new { id = pid }, tx);
            }

            await c.ExecuteAsync($"INSERT {D}({pk}, MaSP, SoLuong, {priceCol}) VALUES (@id, @MaSP, @SoLuong, @DonGia)",
                lines.Select(l => new { id = pid, l.MaSP, l.SoLuong, l.DonGia }), tx);

            var after = await c.ExecuteScalarAsync<int>(NegSql, null, tx);
            if (after > before)
            {
                var bad = (await c.QueryAsync<string>(
                    $"SELECT TOP 3 TenSP + N' tại ' + TenKho + N' (còn ' + CAST(Ton AS NVARCHAR(12)) + N')' FROM ({Sql.Stock}) t WHERE Ton < 0", null, tx)).ToList();
                tx.Rollback();
                return await Back("Không đủ tồn kho: " + string.Join("; ", bad));
            }

            tx.Commit();
        }
        catch (SqlException ex) { return await Back(SqlMsg(ex)); }

        FlashOk(nhap ? "Đã lưu phiếu nhập." : "Đã lưu phiếu xuất.");
        return RedirectToAction(nameof(Index), new { loai });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string loai, int id)
    {
        if (!OkLoai(loai)) return NotFound();
        if (CamSua(loai))
        {
            FlashErr("Thủ kho không được xóa phiếu.");
            return RedirectToAction(nameof(Index), new { loai });
        }
        var nhap = loai == "nhap";
        string H = nhap ? "PhieuNhap" : "PhieuXuat", D = nhap ? "ChiTietPhieuNhap" : "ChiTietPhieuXuat", pk = nhap ? "MaPN" : "MaPX";
        try
        {
            await using var c = db.Open();
            if (!await ThuocKho(c, loai, id)) return Forbid();
            if (await DonLienKet(c, loai, id) is string ma)
            {
                FlashErr($"Phiếu xuất này được tạo từ đơn bán lẻ {ma}, không thể xóa trực tiếp.");
                return RedirectToAction(nameof(Index), new { loai });
            }
            using var tx = c.BeginTransaction();
            var before = await c.ExecuteScalarAsync<int>(NegSql, null, tx);
            await c.ExecuteAsync($"DELETE {D} WHERE {pk}=@id", new { id }, tx);
            await c.ExecuteAsync($"DELETE {H} WHERE {pk}=@id", new { id }, tx);
            var after = await c.ExecuteScalarAsync<int>(NegSql, null, tx);
            if (after > before)
            {
                tx.Rollback();
                FlashErr("Không thể xóa phiếu này vì tồn kho sẽ bị âm (hàng của phiếu đã được xuất đi).");
            }
            else
            {
                tx.Commit();
                FlashOk("Đã xóa phiếu.");
            }
        }
        catch (SqlException ex) { FlashErr(SqlMsg(ex)); }
        return RedirectToAction(nameof(Index), new { loai });
    }
}
