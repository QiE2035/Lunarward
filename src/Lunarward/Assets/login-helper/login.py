#!/usr/bin/env python3
"""Lunarward 登录助手。

作用：用 WebKitGTK 打开米游社 / HoYoLAB 登录页，把登录后的 cookie 导出为 JSON，
供 Lunarward 的 WebLoginService 读取（Linux 版 WebView2 没有 CookieManager，见 D29/D32）。

用法：
    python3 login.py --url <登录页 URL> --output <cookies.json>

行为：窗口关闭时导出 cookie 并退出；若一直不关，最长等待 --timeout 秒（默认 300）。
"""
import argparse
import json
import os
import sys
import time

import gi

gi.require_version("Gtk", "3.0")
gi.require_version("WebKit2", "4.1")
from gi.repository import GLib, Gtk, WebKit2  # noqa: E402


def parse_args(argv):
    parser = argparse.ArgumentParser(description="Lunarward login helper")
    parser.add_argument("--url", required=True, help="登录页地址")
    parser.add_argument("--output", required=True, help="cookie JSON 输出路径")
    parser.add_argument("--timeout", type=int, default=300, help="最长等待秒数")
    return parser.parse_args(argv)


def main(argv):
    args = parse_args(argv)
    output = os.path.abspath(args.output)
    os.makedirs(os.path.dirname(output) or ".", exist_ok=True)
    db = output + ".db"

    context = WebKit2.WebContext.get_default()
    manager = context.get_cookie_manager()
    manager.set_persistent_storage(db, WebKit2.CookiePersistentStorage.SQLITE)

    view = WebKit2.WebView()
    win = Gtk.Window(title="Lunarward 登录")
    win.set_default_size(1100, 760)
    win.add(view)

    state = {"saved": False}

    def export_cookies(_win=None):
        if state["saved"]:
            return False
        state["saved"] = True
        manager.get_cookies(args.url, None, on_cookies, None)
        return False

    def write_payload(cookies):
        data = {c.get_name(): c.get_value() for c in cookies}
        payload = {"Cookies": data, "Time": time.strftime("%Y-%m-%dT%H:%M:%S")}
        with open(output, "w", encoding="utf-8") as f:
            json.dump(payload, f, ensure_ascii=False, indent=2)
        return True

    def on_cookies(mgr, result, _):
        try:
            cookies = mgr.get_cookies_finish(result)
        except Exception as exc:  # noqa: BLE001
            print(f"[login] 读取 cookie 失败: {exc}", file=sys.stderr)
            cookies = []
        write_payload(cookies)
        print(f"[login] 已导出 {len(cookies)} 条 cookie -> {output}")
        Gtk.main_quit()

    LOGIN_MARKERS = ("ltuid_v2", "ltmid_v2", "ltoken_v2", "cookie_token_v2",
                     "account_id", "cookie_token", "ltuid")

    def poll(_=None):
        """登录成功后主动落盘，用户不必关窗口。"""
        def done(mgr, result, _):
            try:
                cookies = mgr.get_cookies_finish(result)
            except Exception:  # noqa: BLE001
                return
            names = {c.get_name() for c in cookies}
            if names & set(LOGIN_MARKERS):
                write_payload(cookies)
                print(f"[login] 检测到登录 cookie，已写入 {output}")
                Gtk.main_quit()
        manager.get_cookies(args.url, None, done, None)
        return True

    win.connect("destroy", export_cookies)
    view.load_uri(args.url)
    win.show_all()

    GLib.timeout_add_seconds(2, poll)
    GLib.timeout_add_seconds(args.timeout, lambda: (export_cookies(), False)[1])
    Gtk.main()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
