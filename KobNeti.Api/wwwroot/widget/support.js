(function () {
  "use strict";

  var script = document.currentScript;
  if (!script) return;

  var tenantKey = script.getAttribute("data-tenant-key") || "";
  var apiBase = (script.getAttribute("data-api-base") || "").replace(/\/$/, "");
  if (!tenantKey || !apiBase) {
    console.warn("[KobNeti] widget requires data-tenant-key and data-api-base");
    return;
  }

  var mode = (script.getAttribute("data-mode") || "ticket").toLowerCase();
  var pageUrl = window.location.href;
  var accountId = script.getAttribute("data-account-id") || "";

  function el(tag, attrs, children) {
    var node = document.createElement(tag);
    if (attrs) Object.keys(attrs).forEach(function (k) {
      if (k === "text") node.textContent = attrs[k];
      else if (k === "html") node.innerHTML = attrs[k];
      else node.setAttribute(k, attrs[k]);
    });
    (children || []).forEach(function (c) { if (c) node.appendChild(c); });
    return node;
  }

  function postTicket(payload) {
    return fetch(apiBase + "/api/Help/ticket", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Tenant-Key": tenantKey
      },
      body: JSON.stringify(payload)
    }).then(function (r) { return r.json(); });
  }

  var panel = el("div", { id: "kobneti-ticket-panel", style: "display:none;position:fixed;right:20px;bottom:84px;width:320px;max-width:calc(100vw - 32px);background:#111827;color:#e5e7eb;border:1px solid #374151;border-radius:12px;box-shadow:0 12px 40px rgba(0,0,0,.35);z-index:2147483000;font:14px/1.4 system-ui,sans-serif;padding:16px;" });
  panel.appendChild(el("h3", { text: "Contact support", style: "margin:0 0 12px;font-size:16px;" }));

  function field(label, name, type) {
    var wrap = el("label", { style: "display:block;margin-bottom:10px;font-size:12px;color:#9ca3af;" });
    wrap.appendChild(document.createTextNode(label));
    var input = el(type === "textarea" ? "textarea" : "input", {
      name: name,
      style: "display:block;width:100%;margin-top:4px;box-sizing:border-box;border-radius:8px;border:1px solid #374151;background:#0b1220;color:#e5e7eb;padding:8px;"
    });
    if (type === "textarea") input.setAttribute("rows", "4");
    else input.setAttribute("type", type || "text");
    wrap.appendChild(input);
    return wrap;
  }

  var form = el("form");
  form.appendChild(field("Name", "name", "text"));
  form.appendChild(field("Email", "email", "email"));
  form.appendChild(field("Subject", "subject", "text"));
  form.appendChild(field("Message", "message", "textarea"));
  var status = el("p", { style: "min-height:18px;margin:0 0 8px;font-size:12px;color:#93c5fd;" });
  form.appendChild(status);
  var submit = el("button", { type: "submit", text: "Send", style: "width:100%;border:0;border-radius:8px;background:#2563eb;color:#fff;padding:10px;font-weight:600;cursor:pointer;" });
  form.appendChild(submit);
  panel.appendChild(form);

  form.addEventListener("submit", function (e) {
    e.preventDefault();
    var data = new FormData(form);
    status.textContent = "Sending…";
    submit.disabled = true;
    postTicket({
      name: data.get("name"),
      email: data.get("email"),
      category: "general",
      subject: data.get("subject"),
      message: data.get("message"),
      pageUrl: pageUrl,
      accountId: accountId || null
    }).then(function (body) {
      if (body && body.success) {
        status.textContent = "Ticket submitted. Thank you!";
        form.reset();
      } else {
        status.textContent = (body && body.message) || "Unable to submit.";
      }
    }).catch(function () {
      status.textContent = "Network error.";
    }).finally(function () {
      submit.disabled = false;
    });
  });

  var btn = el("button", {
    type: "button",
    text: "Support",
    style: "position:fixed;right:20px;bottom:20px;z-index:2147483000;border:0;border-radius:999px;background:#2563eb;color:#fff;padding:12px 18px;font:600 14px system-ui,sans-serif;cursor:pointer;box-shadow:0 8px 24px rgba(37,99,235,.4);"
  });
  btn.addEventListener("click", function () {
    panel.style.display = panel.style.display === "none" ? "block" : "none";
  });

  if (mode === "ticket" || mode === "both") {
    document.body.appendChild(panel);
    document.body.appendChild(btn);
  }
})();
