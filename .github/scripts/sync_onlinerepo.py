"""按脚本里的 [ScriptType(...)] 同步三个 OnlineRepo.json。

python .github/scripts/sync_onlinerepo.py              # 写入
python .github/scripts/sync_onlinerepo.py --dry-run    # 只打印 diff
python .github/scripts/sync_onlinerepo.py --selftest   # 解析器自检
"""
import difflib
import json
import os
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import quote, unquote

RAW = "https://raw.githubusercontent.com/Codaaaaaa/KodakkuScripts/main/"
REPOS = {"Scripts": "OnlineRepo.json", "Dev": "Dev/OnlineRepo.json", "Global": "Global/OnlineRepo.json"}
KEYS = ["Name", "Guid", "Version", "Author", "TerritoryIds", "DownloadUrl", "Note", "UpdateInfo"]

WS = r"(?:\s|//[^\n]*|/\*[\s\S]*?\*/)*"  # 空白和注释
ATOM = re.compile(WS + r'''(?:
    (?P<list>\[[^\]]*\])                                 # [1, 2]
  | (?P<rd>\$*)(?P<q>"{3,})(?P<rb>[\s\S]*?)(?P=q)(?!")    # """原始字符串"""
  | (?P<gd>\$?)"(?P<gb>(?:[^"\\\n]|\\.)*)"                # "普通字符串"
  | (?P<id>[\w.]+)                                       # const 名
)''', re.X)
PLUS = re.compile(WS + r"\+")
ARG = re.compile(WS + r"(\w+)" + WS + ":")
SEP = re.compile(WS + r"([,)])")
ESC = {"n": "\n", "t": "\t", "r": "\r", "0": "\0", "a": "\a", "b": "\b", "f": "\f", "v": "\v", "e": "\x1b"}


def bad(src, pos):
    return ValueError("看不懂这里: " + " ".join(src[pos:pos + 60].split()))


def text(m, const):
    """字符串字面量求值；插值 {X} 只认同文件里的 const。"""
    if m["q"]:
        n, s = len(m["rd"]), m["rb"]
        if "\n" in s:  # 多行原始字符串：去掉首尾行，按结束 """ 前的缩进去缩进
            lines = s.split("\n")
            s = "\n".join(line[len(lines[-1]):] for line in lines[1:-1])
        hole = re.escape("{" * n) + r"([^{}]*)" + re.escape("}" * n)
        return re.sub(hole, lambda h: const(h[1].strip()), s) if n else s
    pat = r"\\(u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|x[0-9a-fA-F]{1,4}|.)" + (r"|\{\{|\}\}|\{([^{}]*)\}" if m["gd"] else "")

    def sub(t):
        if t[1]:
            return chr(int(t[1][1:], 16)) if len(t[1]) > 1 else ESC.get(t[1], t[1])
        return t[0][0] if t[2] is None else const(t[2].strip())
    return re.sub(pat, sub, m["gb"])


def value(atoms, const):
    vals = [[int(x) for x in re.findall(r"\d+", m["list"])] if m["list"]
            else const(m["id"]) if m["id"] else text(m, const) for m in atoms]
    return vals[0] if len(vals) == 1 else "".join(vals)


def scan(src, pos):
    """读一个 atom (+ atom)*，返回 (atoms, 结束位置)。"""
    atoms = []
    while True:
        m = ATOM.match(src, pos)
        if not m:
            raise bad(src, pos)
        atoms.append(m)
        plus = PLUS.match(src, m.end())
        if not plus:
            return atoms, m.end()
        pos = plus.end()


def parse(src):
    """返回 get(参数名, 默认值) 取 [ScriptType] 的参数；文件里没有 ScriptType 返回 None。"""
    m = re.search(r"^[ \t]*\[\s*ScriptType\s*\(", src, re.M)
    if not m:
        return None
    decl, cache, args, pos = {}, {}, {}, m.end()
    for c in re.finditer(r"\bconst\s+string\s+(\w+)\s*=", src):
        decl.setdefault(c[1], c.end())

    def const(name):
        if name not in decl:
            raise ValueError(f"找不到 const {name}")
        if name not in cache:
            cache[name] = value(scan(src, decl[name])[0], const)
        return cache[name]

    while True:
        a = ARG.match(src, pos)
        if not a:
            raise bad(src, pos)
        args[a[1]], pos = scan(src, a.end())
        sep = SEP.match(src, pos)
        if not sep:
            raise bad(src, pos)
        pos = sep.end()
        if sep[1] == ")":
            return lambda key, default=None: value(args[key], const) if key in args else default


def main():
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    os.chdir(Path(__file__).resolve().parents[2])
    tracked = subprocess.run(["git", "ls-files", "-z", *REPOS], capture_output=True, check=True).stdout.decode().split("\0")
    seen = {}
    for folder, repo_path in REPOS.items():
        with open(repo_path, encoding="utf-8") as f:
            old = f.read()
        repo = json.loads(old)
        by_guid = {e["Guid"]: e for e in repo}
        mine = set()
        for path in (p for p in tracked if p.startswith(folder + "/") and p.endswith(".cs")):
            try:
                with open(path, encoding="utf-8-sig") as f:
                    get = parse(f.read())
                if not get:
                    continue
                want = {"Name": get("name"), "Guid": get("guid"), "Version": get("version"), "Author": get("author", ""),
                        "TerritoryIds": get("territorys", []), "DownloadUrl": RAW + quote(path),
                        "Note": get("note"), "UpdateInfo": get("updateInfo")}
            except ValueError as ex:
                sys.exit(f"::error::{path}: {ex}")
            guid = want["Guid"]
            if not (guid and want["Name"] and want["Version"]):
                sys.exit(f"::error::{path}: ScriptType 缺 guid/name/version")
            if guid in seen:
                sys.exit(f"::error::GUID 重复: {path} 和 {seen[guid]}")
            seen[guid] = path
            mine.add(guid)
            for k in ("Note", "UpdateInfo"):
                if want[k] is None:
                    del want[k]  # 脚本没写，保留 json 里手写的
            e = by_guid.get(guid)
            if e is None:
                want.setdefault("UpdateInfo", "")
                repo.append(want)
                print(f"{path}: 新增 v{want['Version']}")
                continue
            if unquote(e.get("DownloadUrl", "")) == RAW + path:
                del want["DownloadUrl"]  # 同一个文件，只是 URL 编码写法不同
            for k, v in want.items():
                if e.get(k) != v:
                    print(f"{path}: {k}" + ("" if k in ("Note", "UpdateInfo") else f" {e.get(k)} → {v}"))
                    e[k] = v
        for g, e in by_guid.items():
            if g not in mine:
                print(f"::warning::{repo_path}: {e['Name']} 找不到对应脚本", file=sys.stderr)
        # 新补的 Note 会排到 UpdateInfo 后面，按固定顺序重排
        repo = [dict(sorted(e.items(), key=lambda kv: KEYS.index(kv[0]) if kv[0] in KEYS else len(KEYS))) for e in repo]
        # 保持原来的格式：TerritoryIds 写一行
        new = re.sub(r"\[\n[\d,\s]*\]", lambda m: "[" + ", ".join(re.findall(r"\d+", m[0])) + "]",
                     json.dumps(repo, indent=2, ensure_ascii=False)) + "\n"
        if new == old:
            continue
        if "--dry-run" in sys.argv:
            sys.stdout.writelines(difflib.unified_diff(old.splitlines(True), new.splitlines(True), repo_path, repo_path))
        else:
            with open(repo_path, "w", encoding="utf-8", newline="\n") as f:
                f.write(new)


def selftest():
    get = parse(r'''
//[ScriptType(name: "旧的")]
[ScriptType(name: "N (U) \"q\" 中", territorys: [1, 22], guid: "g", // 注释
    version: Version, author: A + "b", updateInfo: Info)]
class C
{
    const string A = "a";
    private const string Version = "1.0";
    const string Info =
        $"""
        {Version}
          x
        """;
}''')
    assert get("name") == 'N (U) "q" 中', get("name")
    assert (get("territorys"), get("guid"), get("author"), get("note")) == ([1, 22], "g", "ab", None)
    assert get("updateInfo") == "1.0\n  x", repr(get("updateInfo"))
    print("selftest ok")


if __name__ == "__main__":
    selftest() if "--selftest" in sys.argv else main()
