(function () {
  function slotElements(id) {
    var out = [];
    var nodes = document.querySelectorAll('[data-xc-slot]');
    for (var i = 0; i !== nodes.length; i++) {
      if (nodes[i].getAttribute('data-xc-slot') === id) { out.push(nodes[i]); }
    }
    return out;
  }

  function findTarget(item) {
    var i;
    if (item.slot) {
      var bySlot = slotElements(item.slot);
      if (bySlot.length) { return bySlot[0]; }
    }
    var fb = item.fb || [];
    for (i = 0; i !== fb.length; i++) {
      var el = document.getElementById(fb[i]);
      if (el) {
        if (el.hasAttribute('data-xc-occupied')) { continue; }
        return el;
      }
    }
    return null;
  }

  function build(item, layout) {
    var wrap = document.createElement('div');
    wrap.setAttribute('data-xc-asset', item.k);
    var jc = item.a === 'center' ? 'center' : (item.a === 'right' ? 'flex-end' : 'flex-start');
    var css = 'display:flex;align-items:center;justify-content:' + jc + ';';
    if (layout === 'block-clear') { css += 'clear:both;'; }
    if (layout === 'float-left') { css += 'float:left;margin:0 8px 4px 0;'; }
    wrap.style.cssText = css;
    var img = document.createElement('img');
    img.alt = '';
    img.src = item.src;
    img.style.cssText = (item.w ? 'width:' + item.w + 'px;' : 'width:auto;')
      + (item.h ? 'height:' + item.h + 'px;' : 'height:auto;')
      + 'max-width:100%;object-fit:contain;display:block;';
    wrap.appendChild(img);
    return wrap;
  }

  function replace(el, item) {
    el.src = item.src;
    el.removeAttribute('srcset');
    el.setAttribute('data-xc-asset', item.k);
    el.style.objectFit = 'contain';
    el.style.objectPosition = item.a === 'center' ? 'center center' : (item.a === 'right' ? 'right center' : 'left center');
    if (item.w) { el.style.width = item.w + 'px'; }
    if (item.h) { el.style.height = item.h + 'px'; }
    if (item.w) { if (!item.h) { el.style.height = 'auto'; } }
    if (item.h) { if (!item.w) { el.style.width = 'auto'; } }
  }

  function run() {
    var holder = document.querySelector('script[data-xc-overrides-data]');
    if (!holder) { return; }
    var data;
    try { data = JSON.parse(holder.textContent || ''); } catch (e) { return; }
    if (!data || data.v !== 2 || !data.items) { return; }
    var cleared = [];
    var replaced = [];
    var kinds = {};
    var i, j;
    for (i = 0; i !== data.items.length; i++) {
      var item = data.items[i];
      var el = findTarget(item);
      if (!el) { continue; }
      kinds[item.k] = true;
      if (el.hasAttribute('data-xc-placeholder')) {
        var group = slotElements(el.getAttribute('data-xc-slot') || '');
        if (replaced.indexOf(group[0]) === -1) {
          for (j = 0; j !== group.length; j++) { replace(group[j], item); replaced.push(group[j]); }
        } else {
          var last = group[group.length - 1];
          last.parentNode.insertBefore(build(item, 'block-clear'), last.nextSibling);
        }
        continue;
      }
      if ((el.tagName || '').toUpperCase() === 'IMG') {
        if (replaced.indexOf(el) === -1) {
          replace(el, item);
          replaced.push(el);
        } else {
          el.parentNode.insertBefore(build(item, 'block-clear'), el.nextSibling);
        }
        continue;
      }
      if (cleared.indexOf(el) === -1) {
        while (el.firstChild) { el.removeChild(el.firstChild); }
        cleared.push(el);
      }
      el.appendChild(build(item, el.getAttribute('data-xc-layout') || 'block'));
    }
    var removed = {};
    var hide = data.hide || [];
    for (i = 0; i !== hide.length; i++) { removed[hide[i]] = true; }
    var placeholders = document.querySelectorAll('[data-xc-placeholder]');
    for (i = 0; i !== placeholders.length; i++) {
      var p = placeholders[i];
      var k = p.getAttribute('data-xc-placeholder');
      var drop = kinds[k] === true;
      if (removed[k] === true) { if (p.hasAttribute('data-xc-removable')) { drop = true; } }
      if (drop) {
        if (replaced.indexOf(p) === -1) {
          p.style.visibility = 'hidden';
          p.setAttribute('data-xc-hidden', '1');
        }
      }
    }
  }

  if (document.readyState === 'complete') { run(); } else { window.addEventListener('load', run); }
})();
