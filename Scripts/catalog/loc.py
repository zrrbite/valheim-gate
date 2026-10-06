import csv, glob, io, json, os, sys
S = sys.argv[1]
out = {}
src = {}
for f in sorted(glob.glob(os.path.join(S, "work/text/localization*.txt"))):
    txt = open(f, encoding="utf-8-sig", errors="replace").read()
    rows = list(csv.reader(io.StringIO(txt)))
    n = 0
    for row in rows[1:]:
        if len(row) < 2 or not row[0].strip():
            continue
        k = row[0].strip()
        en = row[1]
        if k in out and out[k] and not en:
            continue
        out[k] = en; src[k] = os.path.basename(f).split("__")[0]; n += 1
    print(os.path.basename(f), len(rows), n)
json.dump({"en": out, "src": src}, open(os.path.join(S, "work/loc.json"), "w"), ensure_ascii=False, indent=0)
print("total keys", len(out))
