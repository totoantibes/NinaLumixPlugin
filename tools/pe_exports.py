# Print the exported function names of a PE DLL, one per line.
#
# Pure stdlib PE walk: DOS header -> NT headers -> optional header -> data directory entry 0
# (export table) -> AddressOfNames -> the name strings. Handles PE32 and PE32+.
#
# Used by verify-entrypoints.ps1 to prove every DllImport in the plugin names a symbol that
# actually exists in Panasonic's Lmxptpif.dll. An EntryPointNotFoundException is a deploy that
# bricks the camera, and it is fully checkable here with no device and no NINA.
import struct
import sys


def read_u16(b, o):
    return struct.unpack_from("<H", b, o)[0]


def read_u32(b, o):
    return struct.unpack_from("<I", b, o)[0]


def main(path):
    with open(path, "rb") as f:
        b = f.read()

    if b[:2] != b"MZ":
        sys.exit("not a PE file: no MZ signature")
    pe = read_u32(b, 0x3C)
    if b[pe:pe + 4] != b"PE\0\0":
        sys.exit("not a PE file: no PE signature at e_lfanew")

    n_sections = read_u16(b, pe + 6)
    opt_size = read_u16(b, pe + 20)
    opt = pe + 24
    magic = read_u16(b, opt)
    if magic == 0x10B:
        dd = opt + 96          # PE32 data directory offset
    elif magic == 0x20B:
        dd = opt + 112         # PE32+ data directory offset
    else:
        sys.exit("unknown optional header magic 0x%X" % magic)

    exp_rva = read_u32(b, dd)
    exp_size = read_u32(b, dd + 4)
    if exp_rva == 0 or exp_size == 0:
        sys.exit("DLL has no export directory")

    # Section table follows the optional header; needed to map RVAs to file offsets.
    sections = []
    st = opt + opt_size
    for i in range(n_sections):
        s = st + i * 40
        va = read_u32(b, s + 12)
        vsize = read_u32(b, s + 8)
        raw = read_u32(b, s + 20)
        rsize = read_u32(b, s + 16)
        sections.append((va, max(vsize, rsize), raw))

    def off(rva):
        for va, size, raw in sections:
            if va <= rva < va + size:
                return raw + (rva - va)
        sys.exit("RVA 0x%X is in no section" % rva)

    e = off(exp_rva)
    n_names = read_u32(b, e + 24)
    names_rva = read_u32(b, e + 32)
    if n_names == 0:
        sys.exit("export directory lists 0 named exports")

    base = off(names_rva)
    out = []
    for i in range(n_names):
        s = off(read_u32(b, base + i * 4))
        end = b.index(b"\0", s)
        out.append(b[s:end].decode("ascii", "replace"))

    for name in out:
        print(name)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("usage: pe_exports.py <path-to-dll>")
    main(sys.argv[1])
