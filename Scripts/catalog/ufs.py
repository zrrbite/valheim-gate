"""Minimal UnityFS bundle + SerializedFile (v22+) reader with type-tree decoding. Data only.

Bundle directory comes from the original bundle header; object data is read from the flat
file written by Scripts/unpack_bundle.py (blocks concatenated), via mmap.
"""
import lzma
import mmap
import os
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
COMMON = None


def common_strings():
    global COMMON
    if COMMON is None:
        d = open(os.path.join(HERE, "..", "work", "commonstrings.bin"), "rb").read()
        COMMON = {}
        off = 0
        for s in d.split(b"\0"):
            COMMON[off] = s.decode()
            off += len(s) + 1
    return COMMON


def lz4_block(src, usize):
    dst = bytearray()
    i, n = 0, len(src)
    while i < n:
        token = src[i]; i += 1
        lit = token >> 4
        if lit == 15:
            while True:
                b = src[i]; i += 1; lit += b
                if b != 255:
                    break
        dst += src[i:i + lit]; i += lit
        if i >= n:
            break
        off = src[i] | (src[i + 1] << 8); i += 2
        ml = token & 15
        if ml == 15:
            while True:
                b = src[i]; i += 1; ml += b
                if b != 255:
                    break
        ml += 4
        start = len(dst) - off
        if off >= ml:
            dst += dst[start:start + ml]
        else:
            pat = bytes(dst[start:])
            dst += (pat * (ml // off + 1))[:ml]
    return bytes(dst)


def decompress(kind, src, usize):
    if kind == 0:
        return src
    if kind == 1:
        props = src[0]; lc, rest = props % 9, props // 9; lp, pb = rest % 5, rest // 5
        ds = struct.unpack("<I", src[1:5])[0]
        return lzma.LZMADecompressor(format=lzma.FORMAT_RAW, filters=[
            {"id": lzma.FILTER_LZMA1, "dict_size": ds, "lc": lc, "lp": lp, "pb": pb}]).decompress(src[5:], max_length=usize)
    return lz4_block(src, usize)


def bundle_dir(path):
    with open(path, "rb") as f:
        head = f.read(4096)
        assert head.startswith(b"UnityFS\0")
        pos = 8
        version = struct.unpack(">I", head[pos:pos + 4])[0]; pos += 4
        pos = head.index(b"\0", pos) + 1
        pos = head.index(b"\0", pos) + 1
        _, csize, usize, flags = struct.unpack(">qIII", head[pos:pos + 20]); pos += 20
        if version >= 7:
            pos = (pos + 15) & ~15
        if flags & 0x80:
            f.seek(-csize, 2); binfo = f.read(csize)
        else:
            f.seek(pos); binfo = f.read(csize)
    binfo = decompress(flags & 0x3F, binfo, usize)
    count = struct.unpack(">i", binfo[16:20])[0]
    p = 20 + count * 10
    ncount = struct.unpack(">i", binfo[p:p + 4])[0]; p += 4
    nodes = []
    for _ in range(ncount):
        off, size, fl = struct.unpack(">qqI", binfo[p:p + 20]); p += 20
        e = binfo.index(b"\0", p); name = binfo[p:e].decode(); p = e + 1
        nodes.append((off, size, fl, name))
    return nodes


class TNode:
    __slots__ = ("type", "name", "size", "level", "flags", "meta", "children")

    def __init__(self, t, n, s, l, f, m):
        self.type, self.name, self.size, self.level, self.flags, self.meta = t, n, s, l, f, m
        self.children = []


PRIM = {
    "SInt8": ("b", 1), "UInt8": ("B", 1), "char": ("B", 1), "bool": ("?", 1),
    "SInt16": ("h", 2), "short": ("h", 2), "UInt16": ("H", 2), "unsigned short": ("H", 2),
    "SInt32": ("i", 4), "int": ("i", 4), "UInt32": ("I", 4), "unsigned int": ("I", 4), "Type*": ("I", 4),
    "SInt64": ("q", 8), "long long": ("q", 8), "UInt64": ("Q", 8), "unsigned long long": ("Q", 8),
    "FileSize": ("Q", 8), "float": ("f", 4), "double": ("d", 8),
}


class SFile:
    def __init__(self, buf, base, size, name=""):
        self.buf, self.base, self.name = buf, base, name
        d = buf
        p = base
        _, _, ver, _ = struct.unpack(">IIII", d[p:p + 16]); p += 16
        self.ver = ver
        assert ver >= 22, ver
        endian = d[p]; p += 4
        meta_size, fsize, data_off, _ = struct.unpack(">IQQQ", d[p:p + 28]); p += 28
        self.data_off = base + data_off
        e = "<" if endian == 0 else ">"
        self.e = e
        q = d.find(b"\0", p); self.unity = d[p:q].decode(); p = q + 1
        self.platform = struct.unpack(e + "i", d[p:p + 4])[0]; p += 4
        has_tt = d[p]; p += 1
        nt = struct.unpack(e + "i", d[p:p + 4])[0]; p += 4
        cs = common_strings()
        self.types = []
        for _ in range(nt):
            cid, stripped, sidx = struct.unpack(e + "iBh", d[p:p + 7]); p += 7
            if cid == 114 or cid < 0:
                p += 16
            p += 16
            root = None
            if has_tt:
                nn, sb = struct.unpack(e + "ii", d[p:p + 8]); p += 8
                raw = d[p:p + nn * 32]; p += nn * 32
                sbuf = d[p:p + sb]; p += sb

                def gs(o):
                    if o & 0x80000000:
                        return cs.get(o & 0x7FFFFFFF, "?")
                    q2 = sbuf.index(b"\0", o)
                    return sbuf[o:q2].decode("utf-8", "replace")
                stack = []
                for k in range(nn):
                    v, lvl, tf, to, no, bs, idx, mf, _h = struct.unpack(e + "HBBIIiiiQ", raw[k * 32:k * 32 + 32])
                    nd = TNode(gs(to), gs(no), bs, lvl, tf, mf)
                    while stack and stack[-1].level >= lvl:
                        stack.pop()
                    if stack:
                        stack[-1].children.append(nd)
                    else:
                        root = nd
                    stack.append(nd)
                n = struct.unpack(e + "i", d[p:p + 4])[0]; p += 4 + 4 * n
            self.types.append((cid, sidx, root))
        no = struct.unpack(e + "i", d[p:p + 4])[0]; p += 4
        self.objects = []
        for _ in range(no):
            p = (p + 3) & ~3
            pid, off, sz, ti = struct.unpack(e + "qQIi", d[p:p + 24]); p += 24
            self.objects.append((pid, self.data_off + off, sz, ti))
        ns = struct.unpack(e + "i", d[p:p + 4])[0]; p += 4
        self.scripts = []
        for _ in range(ns):
            fi = struct.unpack(e + "i", d[p:p + 4])[0]; p += 4
            p = (p + 3) & ~3
            li = struct.unpack(e + "q", d[p:p + 8])[0]; p += 8
            self.scripts.append((fi, li))
        ne = struct.unpack(e + "i", d[p:p + 4])[0]; p += 4
        self.externals = []
        for _ in range(ne):
            q = d.find(b"\0", p); p = q + 1
            p += 16 + 4
            q = d.find(b"\0", p); self.externals.append(d[p:q].decode()); p = q + 1
        self.by_pid = {o[0]: o for o in self.objects}

    def class_of(self, obj):
        return self.types[obj[3]][0]

    def read(self, obj, maxbytes=None):
        pid, off, sz, ti = obj
        root = self.types[ti][2]
        if root is None:
            return None
        r = _Reader(self.buf, off, self.e, off + sz)
        try:
            return r.val(root)
        except _Stop:
            return r.partial


class _Stop(Exception):
    pass


class _Reader:
    def __init__(self, buf, p, e, end):
        self.b, self.p, self.e, self.end = buf, p, e, end
        self.partial = None

    def align(self):
        self.p = (self.p + 3) & ~3

    def val(self, n):
        t = n.type
        pr = PRIM.get(t)
        if pr is not None and not n.children:
            v = struct.unpack_from(self.e + pr[0], self.b, self.p)[0]
            self.p += pr[1]
            if n.meta & 0x4000:
                self.align()
            return v
        if t == "string":
            ln = struct.unpack_from(self.e + "i", self.b, self.p)[0]; self.p += 4
            if ln < 0 or self.p + ln > self.end:
                raise _Stop()
            v = bytes(self.b[self.p:self.p + ln]).decode("utf-8", "replace"); self.p += ln
            self.align()
            return v
        if t == "TypelessData":
            ln = struct.unpack_from(self.e + "i", self.b, self.p)[0]; self.p += 4 + ln
            if n.meta & 0x4000:
                self.align()
            return "<%d bytes>" % ln
        if t == "ManagedReferencesRegistry" or t == "ReferencedObject":
            raise _Stop()
        if n.flags & 1 or (n.children and n.children[0].flags & 1 and False):
            pass
        if n.children and (n.flags & 1):
            # this node is the Array itself: children[0]=size, children[1]=data
            cnt = struct.unpack_from(self.e + "i", self.b, self.p)[0]; self.p += 4
            if cnt < 0 or cnt > 5_000_000:
                raise _Stop()
            dn = n.children[1]
            dpr = PRIM.get(dn.type)
            if dpr is not None and not dn.children:
                if dn.type in ("UInt8", "SInt8", "char") and cnt > 64:
                    v = "<%d bytes>" % cnt; self.p += cnt
                else:
                    v = list(struct.unpack_from(self.e + dpr[0] * cnt, self.b, self.p)); self.p += dpr[1] * cnt
            else:
                v = [self.val(dn) for _ in range(cnt)]
            if n.meta & 0x4000:
                self.align()
            return v
        if len(n.children) == 1 and n.children[0].flags & 1:
            v = self.val(n.children[0])
            if n.meta & 0x4000:
                self.align()
            return v
        out = {}
        if n.level == 0:
            self.partial = out
        for c in n.children:
            out[c.name] = self.val(c)
            if self.p > self.end:
                raise _Stop()
        if n.meta & 0x4000:
            self.align()
        return out


def open_bundle(bundle_path, bin_path):
    nodes = bundle_dir(bundle_path)
    f = open(bin_path, "rb")
    buf = mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ)
    files = []
    for off, size, fl, name in nodes:
        if fl & 4 or name.startswith("CAB-") and not name.endswith((".resS", ".resource")):
            try:
                files.append(SFile(buf, off, size, name))
            except Exception as ex:
                print("skip", name, ex)
    return nodes, files
