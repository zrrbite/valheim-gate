"""Decompress a UnityFS bundle (LZ4/LZ4HC/LZMA blocks) to one flat file. Pure Python, no deps.

Valheim's prefabs live in StreamingAssets/SoftRef/Bundles, compressed, so a grep for a name the
assembly cannot show (a boss's defeat key, a location) finds only fragments. Unpacked, the strings
are plain: a Character's m_name, boss event and defeat key sit side by side. That is how the Deep
North's names were read on 2026-10-06 (see RESUME). It reads strings; it does not parse objects.

usage: python3 -I Scripts/unpack_bundle.py <bundle> <out>
  e.g. B=".../valheim.app/Contents/Resources/Data/StreamingAssets/SoftRef/Bundles"
       LC_ALL=C grep -lac FrozenKing "$B"/*          # which bundles mention it (fragments do)
       python3 -I Scripts/unpack_bundle.py "$B/e06fccc7" /tmp/x.bin
       LC_ALL=C grep -ao 'defeated_[a-z0-9_]*' /tmp/x.bin | sort | uniq -c
"""
import lzma
import struct
import sys


def lz4_block(src, usize):
    dst = bytearray()
    i, n = 0, len(src)
    while i < n:
        token = src[i]
        i += 1
        lit = token >> 4
        if lit == 15:
            while True:
                b = src[i]
                i += 1
                lit += b
                if b != 255:
                    break
        dst += src[i:i + lit]
        i += lit
        if i >= n:
            break
        off = src[i] | (src[i + 1] << 8)
        i += 2
        ml = token & 15
        if ml == 15:
            while True:
                b = src[i]
                i += 1
                ml += b
                if b != 255:
                    break
        ml += 4
        start = len(dst) - off
        if off >= ml:
            dst += dst[start:start + ml]
        else:
            pat = bytes(dst[start:])
            dst += (pat * (ml // off + 1))[:ml]
    if len(dst) != usize:
        raise ValueError(f"lz4: got {len(dst)} bytes, expected {usize}")
    return bytes(dst)


def lzma_block(src, usize):
    props = src[0]
    lc, rest = props % 9, props // 9
    lp, pb = rest % 5, rest // 5
    dict_size = struct.unpack("<I", src[1:5])[0]
    d = lzma.LZMADecompressor(format=lzma.FORMAT_RAW, filters=[
        {"id": lzma.FILTER_LZMA1, "dict_size": dict_size, "lc": lc, "lp": lp, "pb": pb}])
    return d.decompress(src[5:], max_length=usize)


def decompress(kind, src, usize):
    if kind == 0:
        return src
    if kind == 1:
        return lzma_block(src, usize)
    if kind in (2, 3):
        return lz4_block(src, usize)
    raise ValueError(f"compression {kind}")


def cstr(data, pos):
    end = data.index(b"\0", pos)
    return data[pos:end].decode("utf-8", "replace"), end + 1


def main(path, out):
    data = open(path, "rb").read()
    if not data.startswith(b"UnityFS\0"):
        raise SystemExit("not UnityFS")
    pos = 8
    version = struct.unpack(">I", data[pos:pos + 4])[0]
    pos += 4
    _, pos = cstr(data, pos)
    _, pos = cstr(data, pos)
    _, csize, usize, flags = struct.unpack(">qIII", data[pos:pos + 20])
    pos += 20
    if version >= 7:
        pos = (pos + 15) & ~15
    if flags & 0x80:
        binfo = data[len(data) - csize:]
    else:
        binfo = data[pos:pos + csize]
        pos += csize
    binfo = decompress(flags & 0x3F, binfo, usize)
    count = struct.unpack(">i", binfo[16:20])[0]
    blocks = [struct.unpack(">IIH", binfo[20 + k * 10:30 + k * 10]) for k in range(count)]
    if flags & 0x200:
        pos = (pos + 15) & ~15
    with open(out, "wb") as f:
        for ub, cb, bf in blocks:
            f.write(decompress(bf & 0x3F, data[pos:pos + cb], ub))
            pos += cb
    print(f"{path}: {count} blocks -> {out}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
