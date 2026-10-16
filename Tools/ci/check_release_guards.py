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
# Dev aim hook (BUG-24 automation, Security Admin): only a bare `#if CRASHVERSE_DEV` counts (no OR'd symbols).
HOOK_TOKENS = ["DevAimHookCore", "DevAimHook", "DevAimCommandParser", "DevAimCommand", "DevAimTarget", "DevHookUsed",
               "ReportDevAimResult", "markNonRanked", "dev_aim_cmd.json", "CRASHVERSE_DEV_AIM", "cv_dev_aim", "Crashverse.DevHooks"]
HOOK_SYM = re.compile("|".join(re.escape(t) for t in HOOK_TOKENS))
HOOK_DIR = ("Assets", "_Project", "Scripts", "DevHooks")
HOOK_ASMDEF = "Assets/_Project/Scripts/DevHooks/Crashverse.DevHooks.asmdef"
COMMENT = re.compile(r"^\s*(//|///|\*)")

def rel(p):
    try: return p.relative_to(ROOT)
    except ValueError: return p

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
            if not ok: bad.append(f"{rel(path)}:{n}: '{re.search(sym, code).group(0)}' outside {' / '.join(guards) or '(forbidden)'} guard")
        hm = HOOK_SYM.search(line)
        if hm and not any((not neg) and cond.strip() == "CRASHVERSE_DEV" for cond, neg in stack):
            bad.append(f"{rel(path)}:{n}: dev aim hook symbol '{hm.group(0)}' outside a bare #if CRASHVERSE_DEV")
    return bad

def check_hook_files(root=None):
    """Every DevHooks source must be wholly wrapped in `#if CRASHVERSE_DEV` ... `#endif`; the asmdef must carry
    defineConstraints [CRASHVERSE_DEV] so Unity drops the assembly from release builds."""
    root = root or ROOT; bad = []
    d = root.joinpath(*HOOK_DIR)
    if not d.exists(): return bad
    for f in sorted(d.rglob("*.cs")):
        lines = [l.strip() for l in f.read_text(encoding="utf-8").splitlines() if l.strip()]
        if not lines or lines[0] != "#if CRASHVERSE_DEV" or lines[-1] != "#endif":
            bad.append(f"{f.relative_to(root)}: DevHooks file must start with '#if CRASHVERSE_DEV' and end with '#endif'")
    a = root / HOOK_ASMDEF
    import json
    try: cons = json.loads(a.read_text()).get("defineConstraints", [])
    except Exception: cons = None
    if cons != ["CRASHVERSE_DEV"]: bad.append(f"{HOOK_ASMDEF}: defineConstraints must be exactly [\"CRASHVERSE_DEV\"]")
    return bad

def scan_built(path):
    """Built-output scan: fail if a release APK/AAB (or extracted dir / global-metadata.dat) carries any hook token.
    IL2CPP stores type, method and string-literal names in global-metadata.dat as UTF-8."""
    import zipfile
    path = pathlib.Path(path); blobs = []
    if path.is_dir():
        blobs = [(str(p), p.read_bytes()) for p in path.rglob("global-metadata.dat")]
    elif zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as z:
            blobs = [(n, z.read(n)) for n in z.namelist() if n.endswith("global-metadata.dat")]
    else:
        blobs = [(str(path), path.read_bytes())]
    if not blobs: return [f"{path}: no global-metadata.dat found (not an IL2CPP build?)"]
    bad = []
    for name, data in blobs:
        for t in HOOK_TOKENS:
            if t.encode() in data: bad.append(f"{name}: release metadata contains dev aim hook token '{t}'")
    return bad

def android_manifests(root=None):
    """Main custom manifest plus every Android library plugin manifest (Plugins/Android/**/*.androidlib/AndroidManifest.xml)."""
    root = root or ROOT; d = root / "Assets/Plugins/Android"
    if not d.exists(): return []
    out = [d / "AndroidManifest.xml"] if (d / "AndroidManifest.xml").exists() else []
    out += sorted(p for p in d.rglob("AndroidManifest.xml") if any(x.endswith(".androidlib") for x in p.relative_to(d).parts[:-1]))
    return out

def check_debuggable(root=None):
    """Release must not be debuggable: no manifest may force android:debuggable="true" (dev = Unity Development build)."""
    root = root or ROOT
    return [f"{p.relative_to(root)} must not force android:debuggable=\"true\" (release must not be debuggable)"
            for p in android_manifests(root) if re.search(r'android:debuggable\s*=\s*"true"', p.read_text(errors="replace"))]

def check_config():
    bad = []
    uc = (ROOT / "ProjectSettings/UnityConnectSettings.asset")
    if not uc.exists(): bad.append("ProjectSettings/UnityConnectSettings.asset missing (analytics/crash reporting must be explicitly disabled)")
    else:
        if re.search(r"^\s*m_Enabled:\s*1", uc.read_text(), re.M): bad.append("UnityConnectSettings: a Unity service (analytics/crash/perf/ads/IAP) is enabled")
    man = ROOT / "Assets/Plugins/Android/AndroidManifest.xml"
    bad += check_debuggable()
    if not man.exists() or not re.search(r'com\.google\.android\.gms\.permission\.AD_ID"\s+tools:node="remove"', man.read_text()):
        bad.append("Assets/Plugins/Android/AndroidManifest.xml must remove com.google.android.gms.permission.AD_ID")
    if not re.search(r"^\s*AndroidTargetArchitectures:\s*3\s*$", ps_text(), re.M):
        bad.append("ProjectSettings: AndroidTargetArchitectures must be 3 (ARMv7 | ARM64)")
    if not re.search(r"scriptingBackend:\s*\n\s*Android:\s*1", ps_text()):
        bad.append("ProjectSettings: Android scripting backend must be IL2CPP (1)")
    return bad

def ps_text(): return (ROOT / "ProjectSettings/ProjectSettings.asset").read_text()

def main():
    files = [p for p in (ROOT / "Assets").rglob("*.cs") if "Editor" not in p.parts]
    if len(sys.argv) == 3 and sys.argv[1] == "--built":
        bad = scan_built(sys.argv[2])
        if bad: print("Release build metadata check FAILED:"); print("\n".join(bad)); return 1
        print("Release build metadata check passed (no dev aim hook symbols)."); return 0
    bad = [b for f in files for b in scan(f)] + check_config() + check_hook_files()
    if bad:
        print("Release guard check FAILED (dev-only code would ship in release):"); print("\n".join(bad)); return 1
    print(f"Release guard check passed ({len(files)} files)."); return 0

if __name__ == "__main__":
    sys.exit(main())
