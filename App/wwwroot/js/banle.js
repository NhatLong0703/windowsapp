// Trang đặt mua của nhà bán lẻ: lọc sản phẩm theo kho, tính tiền (giá & tồn do máy chủ cấp và kiểm tra lại khi gửi)
(function () {
    var data = document.getElementById('order-data');
    if (!data) return;
    var stock = JSON.parse(data.dataset.stock), sp = JSON.parse(data.dataset.sp);
    var kho = document.getElementById('kho'), body = document.getElementById('lines');
    var tpl = document.getElementById('line-tpl'), total = document.getElementById('total');
    function fmt(n) { return Math.round(n).toLocaleString('vi-VN'); }

    function fillOptions(sel) {
        var cur = sel.value, s = stock[kho.value] || {};
        sel.innerHTML = '';
        var first = document.createElement('option');
        first.value = ''; first.textContent = kho.value ? '— Chọn sản phẩm —' : '— Hãy chọn kho trước —';
        sel.appendChild(first);
        Object.keys(s).forEach(function (id) {
            var o = document.createElement('option');
            o.value = id;
            o.textContent = sp[id].ten + (sp[id].dvt ? ' (' + sp[id].dvt + ')' : '') + ' — còn ' + s[id];
            sel.appendChild(o);
        });
        if (cur && s[cur]) sel.value = cur;
    }
    function refreshRow(tr) {
        var id = tr.querySelector('[name=maSP]').value, q = tr.querySelector('[name=soLuong]');
        var s = stock[kho.value] || {};
        if (id && s[id]) {
            q.max = s[id];
            if (+q.value > s[id]) q.value = s[id];
            tr.querySelector('.gia').textContent = fmt(sp[id].gia) + ' đ';
            tr.querySelector('.sub').textContent = fmt((+q.value || 0) * sp[id].gia);
        } else {
            q.removeAttribute('max');
            tr.querySelector('.gia').textContent = '0';
            tr.querySelector('.sub').textContent = '0';
        }
    }
    function recalc() {
        var t = 0;
        body.querySelectorAll('tr').forEach(function (tr) {
            refreshRow(tr);
            var id = tr.querySelector('[name=maSP]').value;
            if (id && sp[id]) t += (+tr.querySelector('[name=soLuong]').value || 0) * sp[id].gia;
        });
        total.textContent = fmt(t) + ' đ';
    }
    function add() {
        var n = tpl.content.firstElementChild.cloneNode(true);
        body.appendChild(n);
        fillOptions(n.querySelector('[name=maSP]'));
    }
    kho.addEventListener('change', function () {
        body.querySelectorAll('[name=maSP]').forEach(fillOptions);
        recalc();
    });
    document.getElementById('add-line').addEventListener('click', function () {
        if (!kho.value) { alert('Hãy chọn kho trước.'); return; }
        add(); recalc();
    });
    body.addEventListener('input', recalc);
    body.addEventListener('change', recalc);
    body.addEventListener('click', function (e) {
        var b = e.target.closest('.rm');
        if (b) { b.closest('tr').remove(); recalc(); }
    });
    add(); recalc();
})();
