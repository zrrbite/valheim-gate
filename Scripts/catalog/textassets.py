"""List / extract TextAsset (class 49) objects from a Unity serialized file (format >= 22).
Reads data only. usage: python3 -I textassets.py <file> <outdir> [--list]
"""
import os
import struct
import sys


class R:
    def __init__(self, d, p=0, be=True):
        self.d, self.p, self.be = d, p, be

    def u(self, fmt, n):
        v = struct.unpack((">" if self.be else "<") + fmt, self.d[self.p:self.p + n])[0]
        self.p += n
        return v

    def u8(self): return self.u("B", 1)
    def u16(self): return self.u("H", 2)
    def i32(self): return self.u("i", 4)
    def u32(self): return self.u("I", 4)
    def i64(self): return self.u("q", 8)
    def u64(self): return self.u("Q", 8)

    def cstr(self):
        e = self.d.index(b"\0", self.p)
        s = self.d[self.p:e]
        self.p = e + 1
        return s.decode("utf-8", "replace")

    def align(self, n=4):
        self.p = (self.p + n - 1) & ~(n - 1)


def parse(path):
    d = open(path, "rb").read()
    r = R(d)
    r.u32(); r.u32(); ver = r.u32(); r.u32()
    assert ver >= 22, ver
    endian = r.u8(); r.p += 3
    meta_size = r.u32(); file_size = r.u64(); data_off = r.u64(); r.u64()
    r.be = endian != 0
    unity = r.cstr()
    platform = r.i32()
    has_tt = r.u8()
    ntypes = r.i32()
    types = []
    for _ in range(ntypes):
        cid = r.i32()
        stripped = r.u8()
        script_idx = r.u16() if True else 0
        if cid == 114:
            r.p += 16
        r.p += 16
        if has_tt:
            nodes = r.i32(); sbuf = r.i32()
            r.p += nodes * 32 + sbuf
            if ver >= 21:
                n = r.i32(); r.p += 4 * n
        types.append(cid)
    nobj = r.i32()
    objs = []
    for _ in range(nobj):
        r.align(4)
        pid = r.i64()
        off = r.u64()
        size = r.u32()
        tidx = r.i32()
        objs.append((pid, data_off + off, size, types[tidx]))
    return d, objs, r.be


def main():
    path, outdir = sys.argv[1], sys.argv[2]
    d, objs, be = parse(path)
    os.makedirs(outdir, exist_ok=True)
    from collections import Counter
    print("class counts:", Counter(o[3] for o in objs).most_common(30))
    for pid, off, size, cid in objs:
        if cid != 49:
            continue
        r = R(d, off, be=False)
        n = r.i32(); name = d[r.p:r.p + n].decode("utf-8", "replace"); r.p += n; r.align()
        n2 = r.i32(); body = d[r.p:r.p + n2]
        safe = "".join(c if c.isalnum() or c in "-_." else "_" for c in name)[:80]
        print(f"{pid}\t{size}\t{n2}\t{name}")
        if "--list" not in sys.argv:
            open(os.path.join(outdir, f"{safe}__{pid}.txt"), "wb").write(body)


main()
