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
    if not re.search(r"^\s*AndroidTargetArchitectures:\s*3\s*$", ps_text(), re.M):
        bad.append("ProjectSettings: AndroidTargetArchitectures must be 3 (ARMv7 | ARM64)")
    if not re.search(r"scriptingBackend:\s*\n\s*Android:\s*1", ps_text()):
        bad.append("ProjectSettings: Android scripting backend must be IL2CPP (1)")
    # BUG-20: UI input module must match Active Input Handling (0 legacy, 1 new, 2 both); the legacy module needs its axes.
    pkgs = (ROOT / "Packages/manifest.json").read_text()
    mh = re.search(r"activeInputHandler:\s*(\d)", ps_text()); handler = int(mh.group(1)) if mh else 0
    ui = (ROOT / "Assets/_Project/Scripts/UI/UIFramework.cs").read_text()
    if handler == 1 and "InputSystemUIInputModule" not in ui: bad.append("Input System only, but UI does not use InputSystemUIInputModule")
    if handler != 1 and "com.unity.inputsystem" not in pkgs and "StandaloneInputModule" not in ui:
        bad.append("Legacy Input Manager active but UI has no StandaloneInputModule")
    if handler != 1 and "StandaloneInputModule" in ui:
        im = ROOT / "ProjectSettings/InputManager.asset"
        names = input_axes(im) if im.exists() else set()
        for a in ("Horizontal", "Vertical", "Submit", "Cancel"):
            if a not in names: bad.append(f"ProjectSettings/InputManager.asset missing axis '{a}' (StandaloneInputModule throws, all taps dead: BUG-20)")
    # BUG-24: IL2CPP engine stripping removed BoxCollider/SphereCollider (only added via CreatePrimitive).
    lx = ROOT / "Assets/link.xml"
    if not lx.exists() or not re.search(r'<assembly\s+fullname="UnityEngine\.PhysicsModule"\s+preserve="all"', lx.read_text()):
        bad.append("Assets/link.xml must preserve UnityEngine.PhysicsModule (BUG-24: colliders stripped from the player)")
    return bad

def input_axes(path):
    """M1: parse InputManager.asset structurally: only m_Name entries that are list items directly under m_Axes count."""
    names, in_axes, item_indent = set(), False, None
    for line in path.read_text().splitlines():
        if re.match(r"^\s*m_Axes:\s*$", line): in_axes = True; continue
        if not in_axes: continue
        if re.match(r"^\S", line) or re.match(r"^  \S", line) and not line.startswith("  -"): break  # left m_Axes
        m = re.match(r"^(\s*)- (\w+):\s*(.*)$", line)
        if m: item_indent = len(m.group(1)); cur = {m.group(2): m.group(3)}
        m2 = re.match(r"^(\s*)m_Name:\s*(.+?)\s*$", line)
        if m2 and item_indent is not None and len(m2.group(1)) == item_indent + 2: names.add(m2.group(2))
    return names

def ps_text(): return (ROOT / "ProjectSettings/ProjectSettings.asset").read_text()

def main():
    files = [p for p in (ROOT / "Assets").rglob("*.cs") if "Editor" not in p.parts]
    bad = [b for f in files for b in scan(f)] + check_config()
    if bad:
        print("Release guard check FAILED (dev-only code would ship in release):"); print("\n".join(bad)); return 1
    print(f"Release guard check passed ({len(files)} files)."); return 0

if __name__ == "__main__":
    sys.exit(main())
