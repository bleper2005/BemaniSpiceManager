"""Generate src/BemaniSpiceManager/Data/spice-options.json from spice2x's launcher/options.cpp.

Usage: python tools/gen_spice_options.py path/to/spice2x/launcher/options.cpp
Only option names, titles, descriptions and value lists are taken (no code).
"""
import json, re, sys, pathlib

STR = r'"(?:[^"\\]|\\.)*"'

def unescape(s):
    return bytes(s[1:-1], "utf-8").decode("unicode_escape").encode("latin-1").decode("utf-8")

def strings(text):
    return "".join(unescape(m) for m in re.findall(STR, text))

def field(block, name):
    # value runs until the next top-level ".field =" or end of block
    m = re.search(r'^\s+\.' + name + r'\s*=\s*(.*?)(?=^\s+\.[a-z_]+\s*=|\Z)', block, re.S | re.M)
    return m.group(1).strip().rstrip(",").strip() if m else None

def main(path):
    src = pathlib.Path(path).read_text(encoding="utf-8")
    out = []
    for m in re.finditer(r'c\[launcher::Options::(\w+)\]\s*=\s*\{(.*?)\n    \};', src, re.S):
        key, block = m.group(1), m.group(2)
        o = {"id": key}
        for f in ("title", "name", "desc", "setting_name", "category", "display_name", "aliases"):
            v = field(block, f)
            if v is not None and '"' in v:
                o[f] = strings(v)
        t = field(block, "type")
        o["type"] = t.split("::")[-1] if t else "Text"
        for f in ("hidden", "disabled", "sensitive"):
            if field(block, f) == "true":
                o[f] = True
        g = field(block, "game_name")
        if g:
            o["games"] = [unescape(s) for s in re.findall(STR, g)]
        e = field(block, "elements")
        if e:
            o["elements"] = [[unescape(a), unescape(b)] for a, b in re.findall(r'\{\s*(' + STR + r')\s*,\s*(' + STR + r')\s*\}', e)]
        if "name" in o:
            out.append(o)
    dest = pathlib.Path(__file__).resolve().parent.parent / "src/BemaniSpiceManager/Data/spice-options.json"
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(out, indent=1, ensure_ascii=False), encoding="utf-8")
    print(f"{len(out)} options -> {dest}")

if __name__ == "__main__":
    main(sys.argv[1])
