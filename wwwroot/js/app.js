// Mở hộp thoại thêm/sửa. btn có data-item (JSON) khi sửa.
function openForm(id, btn) {
    var d = document.getElementById(id), f = d.querySelector('form');
    f.reset();
    var item = (btn && btn.dataset.item) ? JSON.parse(btn.dataset.item) : null;
    var t = d.querySelector('[data-new]');
    if (t) t.textContent = item ? t.dataset.edit : t.dataset.new;
    if (item) {
        Object.keys(item).forEach(function (k) {
            var el = f.elements[k];
            if (!el) return;
            var v = item[k];
            if (v === null || v === undefined) v = '';
            if (typeof v === 'string' && /^\d{4}-\d\d-\d\dT/.test(v)) v = v.slice(0, 10);
            el.value = v;
        });
    }
    d.showModal();
}

// Form phiếu nhập/xuất: thêm/bớt dòng, tính thành tiền
(function () {
    var body = document.getElementById('lines');
    if (!body) return;
    var tpl = document.getElementById('line-tpl');
    var total = document.getElementById('total');
    function fmt(n) { return Math.round(n).toLocaleString('vi-VN'); }
    function recalc() {
        var t = 0;
        body.querySelectorAll('tr').forEach(function (tr) {
            var q = +tr.querySelector('[name=soLuong]').value || 0;
            var p = +tr.querySelector('[name=donGia]').value || 0;
            tr.querySelector('.sub').textContent = fmt(q * p);
            t += q * p;
        });
        total.textContent = fmt(t) + ' đ';
    }
    function add() { body.appendChild(tpl.content.firstElementChild.cloneNode(true)); }
    document.getElementById('add-line').addEventListener('click', function () { add(); recalc(); });
    body.addEventListener('input', recalc);
    body.addEventListener('change', function (e) {
        if (e.target.name === 'maSP') {
            var o = e.target.selectedOptions[0];
            var p = e.target.closest('tr').querySelector('[name=donGia]');
            if (o && o.dataset.price && !(+p.value)) p.value = o.dataset.price;
            recalc();
        }
    });
    body.addEventListener('click', function (e) {
        var b = e.target.closest('.rm');
        if (b) { b.closest('tr').remove(); recalc(); }
    });
    if (!body.children.length) add();
    recalc();
})();

// Tự ẩn thông báo sau vài giây
setTimeout(function () { document.querySelectorAll('.flash.ok').forEach(function (e) { e.style.display = 'none'; }); }, 4000);
