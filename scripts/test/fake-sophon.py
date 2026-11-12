#!/usr/bin/env python3
"""假 Sophon 服务（L2 安装链路离线测试用）。

模拟三个端点：
  GET  /downloader/sophon_chunk/api/getBuild      → 清单索引（JSON，外层包 data 节点）
  GET  /manifests/<id>                            → zstd 压缩的 protobuf 清单
  GET  /chunks/<id>                               → zstd 压缩的分块数据

两个版本（tag 9.9.8 / 9.9.9）共用一部分 chunk，用来验证「复用 + 只下新块」：
  v1: Game.exe(chunk A) Data/a.bin(chunk B) Data/old.bin(chunk C)
  v2: Game.exe(chunk A) Data/a.bin(chunk B) Data/new.bin(chunk D)
所以 v1→v2 应复用 A、B，只下载 D。

用法：fake-sophon.py --port 18321
"""
import argparse
import hashlib
import http.server
import json
import socketserver
import struct
import subprocess
import threading

HOST = "127.0.0.1"


def zstd_compress(data: bytes) -> bytes:
    """用系统 zstd 压缩（-q -c 走 stdout）。"""
    proc = subprocess.run(["zstd", "-q", "-c", "-3", "--no-progress"], input=data, stdout=subprocess.PIPE, check=True)
    return proc.stdout


def varint(value: int) -> bytes:
    out = bytearray()
    while True:
        b = value & 0x7F
        value >>= 7
        if value:
            out.append(b | 0x80)
        else:
            out.append(b)
            return bytes(out)


def tag(field: int, wire: int) -> bytes:
    return varint((field << 3) | wire)


def pb_string(field: int, value: str) -> bytes:
    raw = value.encode()
    return tag(field, 2) + varint(len(raw)) + raw


def pb_bytes(field: int, value: bytes) -> bytes:
    return tag(field, 2) + varint(len(value)) + value


def pb_int(field: int, value: int) -> bytes:
    return tag(field, 0) + varint(value)


def build_chunk_msg(chunk_id: str, offset: int, raw: bytes) -> bytes:
    compressed = zstd_compress(raw)
    msg = b""
    msg += pb_string(1, chunk_id)
    msg += pb_string(2, hashlib.md5(raw).hexdigest())          # uncompressed_md5
    msg += pb_int(3, offset)                                   # offset
    msg += pb_int(4, len(compressed))                          # compressed_size
    msg += pb_int(5, len(raw))                                 # uncompressed_size
    msg += pb_string(7, hashlib.md5(compressed).hexdigest())   # compressed_md5
    return msg, compressed


class Version:
    def __init__(self, tag: str, files):
        # files: [(path, chunk_id, raw_bytes)]
        self.tag = tag
        self.files = files
        self.chunks = {}
        self.manifest_raw = b""
        for path, chunk_id, raw in files:
            pass
        self._build()

    def _build(self):
        file_msgs = []
        for path, chunk_id, raw in self.files:
            chunk_msg, compressed = build_chunk_msg(chunk_id, 0, raw)
            self.chunks[chunk_id] = compressed
            fm = b""
            fm += pb_string(1, path)
            fm += pb_bytes(2, chunk_msg)
            fm += pb_int(4, len(raw))
            fm += pb_string(5, hashlib.md5(raw).hexdigest())
            file_msgs.append(fm)
        manifest = b"".join(pb_bytes(1, m) for m in file_msgs)
        self.manifest_raw = manifest
        self.manifest_compressed = zstd_compress(manifest)


class State:
    def __init__(self, port: int):
        self.port = port
        self.versions = {}
        self.order = []
        self.manifests = {}   # manifest id -> compressed bytes

    @property
    def base(self) -> str:
        return f"http://{HOST}:{self.port}"

    def add(self, version: Version):
        mid = f"manifest-{version.tag}"
        self.versions[version.tag] = version
        self.manifests[mid] = version.manifest_compressed
        version.manifest_id = mid
        self.order.append(version.tag)

    def latest(self) -> Version:
        return self.versions[self.order[-1]]


STATE: State = None


class Handler(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):
        pass  # 保持 CI 日志干净

    def _send(self, body: bytes, content_type="application/octet-stream"):
        self.send_response(200)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        path = self.path.split("?")[0]
        if path == "/downloader/sophon_chunk/api/getBuild":
            self._handle_build()
        elif path.startswith("/manifests/"):
            mid = path.rsplit("/", 1)[-1]
            body = STATE.manifests.get(mid)
            if body is None:
                self.send_error(404)
            else:
                self._send(body)
        elif path.startswith("/chunks/"):
            cid = path.rsplit("/", 1)[-1]
            body = None
            for v in STATE.versions.values():
                if cid in v.chunks:
                    body = v.chunks[cid]
                    break
            if body is None:
                self.send_error(404)
            else:
                self._send(body)
        elif path == "/health":
            self._send(b"ok", "text/plain")
        else:
            self.send_error(404)

    def _handle_build(self):
        from urllib.parse import parse_qs, urlparse
        query = parse_qs(urlparse(self.path).query)
        tag = (query.get("tag") or [""])[0]
        version = STATE.versions.get(tag) or STATE.latest()
        manifest_id = version.manifest_id
        payload = {
            "retcode": 0,
            "message": "OK",
            "data": {
                "build_id": f"fake-{version.tag}",
                "tag": version.tag,
                "manifests": [
                    {
                        "category_id": "1",
                        "category_name": "game",
                        "matching_field": "game",
                        "manifest": {
                            "id": manifest_id,
                            "checksum": hashlib.md5(version.manifest_raw).hexdigest(),
                            "compressed_size": len(version.manifest_compressed),
                            "uncompressed_size": len(version.manifest_raw),
                        },
                        "manifest_download": {"url_prefix": f"{STATE.base}/manifests"},
                        "chunk_download": {"url_prefix": f"{STATE.base}/chunks"},
                        "stats": {"compressed_size": str(sum(len(c) for c in version.chunks.values()))},
                    }
                ],
            },
        }
        self._send(json.dumps(payload).encode(), "application/json")

    def do_POST(self):
        # getPatchBuild：离线假服务只回空 data，代表「没有可差分补丁」，让链路走 chunk 复用
        if self.path.split("?")[0] == "/downloader/sophon_chunk/api/getPatchBuild":
            self._send(json.dumps({"retcode": 0, "message": "OK", "data": {"manifests": []}}).encode(), "application/json")
        else:
            self.send_error(404)


def main():
    global STATE
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=18321)
    args = parser.parse_args()

    STATE = State(args.port)
    a = b"A" * 100
    b1 = bytes(range(256)) * 1
    c = b"C" * 128
    d = b"D" * 300
    STATE.add(Version("9.9.8", [
        ("Game.exe", "chunk-a", a),
        ("Data/a.bin", "chunk-b", b1),
        ("Data/old.bin", "chunk-c", c),
    ]))
    STATE.add(Version("9.9.9", [
        ("Game.exe", "chunk-a", a),
        ("Data/a.bin", "chunk-b", b1),
        ("Data/new.bin", "chunk-d", d),
    ]))

    class Server(socketserver.ThreadingMixIn, http.server.HTTPServer):
        daemon_threads = True
        allow_reuse_address = True

    with Server((HOST, args.port), Handler) as httpd:
        print(f"fake sophon on {STATE.base}", flush=True)
        httpd.serve_forever()


if __name__ == "__main__":
    main()
