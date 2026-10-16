#!/usr/bin/env python3
"""B1: fail if dev-only code is reachable without the CRASHVERSE_DEV / DEVELOPMENT_BUILD guard.

Scans Assets/**/*.cs (except Editor/) and tracks #if/#elif/#else/#endif nesting. Any line that mentions a dev-only
symbol must sit inside a branch whose condition names CRASHVERSE_DEV (or DEVELOPMENT_BUILD for QA UI only).
Exit 1 with a list of offending file:line otherwise.
"""
import re, sys, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
# symbol -> allowed guard defines
DEV_SYMBOLS = {
    r"\bLocalBackendService\b": ("CRASHVERSE_DEV",),
    r"\bLocalBackend\b": ("CRASHVERSE_DEV",),
    r"\b(SetTestClock|AdvanceTestClock|ClearTestClock|TestClockActive|SetTestOffset)\b": ("CRASHVERSE_DEV",),
    r"\bSimulateOffline\b": ("CRASHVERSE_DEV",),
    r"\bDevOverride\b": ("CRASHVERSE_DEV",),
    r"\bGrantPremiumPass(Stub|ForTests)\b": ("CRASHVERSE_TESTS",),
    r"\bQADebugModal\b": ("CRASHVERSE_DEV", "DEVELOPMENT_BUILD"),
    r"daily_titan_override": (),  # never allowed anywhere
}
COMMENT = re.compile(r"^\s*(//|///|\*)")

def scan(path):
    bad = []
    stack = []  # list of (condition_text, in_else)
    for n, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        s = line.strip()
        m = re.match(r"#\s*(if|elif|else|endif)\b(.*)", s)
        if m:
            k, cond = m.group(1), m.group(2)
            if k == "if": stack.append((cond, False))
            elif k == "elif" and stack: stack[-1] = (cond, False)
            elif k == "else" and stack: stack[-1] = (stack[-1][0], True)
            elif k == "endif" and stack: stack.pop()
            continue
        if COMMENT.match(line): continue
        code = line.split("//", 1)[0]
        for sym, guards in DEV_SYMBOLS.items():
            if not re.search(sym, code): continue
            ok = any((not neg) and any(g in cond and "!" + g not in cond.replace(" ", "") for g in guards) for cond, neg in stack)
            if not ok: bad.append(f"{path.relative_to(ROOT)}:{n}: '{re.search(sym, code).group(0)}' outside {' / '.join(guards) or '(forbidden)'} guard")
    return bad

def check_config():
    bad = []
    uc = (ROOT / "ProjectSettings/UnityConnectSettings.asset")
    if not uc.exists(): bad.append("ProjectSettings/UnityConnectSettings.asset missing (analytics/crash reporting must be explicitly disabled)")
    else:
        if re.search(r"^\s*m_Enabled:\s*1", uc.read_text(), re.M): bad.append("UnityConnectSettings: a Unity service (analytics/crash/perf/ads/IAP) is enabled")
    man = ROOT / "Assets/Plugins/Android/AndroidManifest.xml"
    if not man.exists() or not re.search(r'com\.google\.android\.gms\.permission\.AD_ID"\s+tools:node="remove"', man.read_text()):
        bad.append("Assets/Plugins/Android/AndroidManifest.xml must remove com.google.android.gms.permission.AD_ID")
    return bad

def main():
    files = [p for p in (ROOT / "Assets").rglob("*.cs") if "Editor" not in p.parts]
    bad = [b for f in files for b in scan(f)] + check_config()
    if bad:
        print("Release guard check FAILED (dev-only code would ship in release):"); print("\n".join(bad)); return 1
    print(f"Release guard check passed ({len(files)} files)."); return 0

if __name__ == "__main__":
    sys.exit(main())
