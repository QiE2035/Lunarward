#!/usr/bin/env python3
"""G0-3 尖峰：WebKitGTK 承载米游社/HoYoLAB 登录页，并验证 cookie 落盘与导出。

只验证「登录页可在 Linux 原生 WebView 中渲染 + cookie 可持久化 + 可导出」这一通路，
不涉及真实账号登录（凭据由用户在 M1 阶段自行输入）。
"""
import json
import os
import sys

import gi

gi.require_version("Gtk", "3.0")
gi.require_version("WebKit2", "4.1")
from gi.repository import GLib, Gtk, WebKit2  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
COOKIE_DB = os.path.join(HERE, "cookies.db")
COOKIE_JSON = os.path.join(HERE, "cookies.json")
DEFAULT_URL = "https://user.miyoushe.com/"
KEEP_MS = int(os.environ.get("KEEP_MS", "12000"))


def dump_cookies(manager, uri, view, loop):
    def on_done(mgr, result, _):
        try:
            cookies = mgr.get_cookies_finish(result)
        except Exception as exc:  # noqa: BLE001
            print(f"[cookie] 导出失败: {exc}")
            cookies = []
        data = []
        for c in cookies:
            data.append({
                "name": c.get_name(),
                "value": (c.get_value() or "")[:80],
                "domain": c.get_domain(),
                "path": c.get_path(),
                "secure": c.get_secure(),
                "http_only": c.get_http_only(),
            })
        with open(COOKIE_JSON, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        print(f"[cookie] 命中 {len(data)} 条，已写入 {COOKIE_JSON}")
        for item in data[:12]:
            print(f"         - {item['name']} @ {item['domain']}")
        print(f"[cookie] 持久化存储文件: {COOKIE_DB} 存在={os.path.exists(COOKIE_DB)}")
        if os.path.exists(COOKIE_DB):
            print(f"[cookie] 存储文件大小: {os.path.getsize(COOKIE_DB)} bytes")
        GLib.timeout_add(KEEP_MS, lambda: (loop.quit(), False)[1])

    manager.get_cookies(uri, None, on_done, None)


def main():
    url = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_URL
    context = WebKit2.WebContext.get_default()
    manager = context.get_cookie_manager()
    manager.set_persistent_storage(COOKIE_DB, WebKit2.CookiePersistentStorage.SQLITE)
    print(f"[init] 持久化 cookie 存储: {COOKIE_DB}")

    view = WebKit2.WebView()
    win = Gtk.Window(title="Lunarward 登录尖峰")
    win.set_default_size(1100, 760)
    win.add(view)
    win.connect("destroy", Gtk.main_quit)
    win.show_all()

    loop = GLib.MainLoop()

    def on_load_changed(v, event):
        if event == WebKit2.LoadEvent.FINISHED:
            print(f"[load] 完成: {v.get_uri()}  title={v.get_title()!r}")
            GLib.timeout_add(3000, lambda: (dump_cookies(manager, url, v, loop), False)[1])

    view.connect("load-changed", on_load_changed)
    view.load_uri(url)
    print(f"[init] 加载 {url}")
    GLib.timeout_add(45000, lambda: (loop.quit(), False)[1])  # 兜底超时
    loop.run()
    print("[done] 退出")


if __name__ == "__main__":
    main()
