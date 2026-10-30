#!/usr/bin/env python3
"""JointBreakPolicy + ranked physics lock (physics/Joint_Break_Policy.md). Exit 1 on any violation.

1. No serialized Unity joint (prefab/scene/asset YAML) has a finite m_BreakForce / m_BreakTorque (kit JNT or otherwise).
2. Runtime code never assigns breakForce / breakTorque from anything but JointBreakPolicy (tests excluded).
3. JointBreakPolicy keeps +infinity and ignores explicit kit values.
4. Ranked determinism lock unchanged: 50 Hz (0.02 s) step, 6 solver iterations, sleep threshold 0.08.
"""
import re, sys, pathlib

ROOT = pathlib.Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else pathlib.Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
POLICY = ASSETS / "_Project/Scripts/Physics/Core/JointBreakPolicy.cs"

def rel(p): return p.relative_to(ROOT)

def check_serialized():
    bad = []
    rx = re.compile(r"^\s*m_Break(Force|Torque):\s*(\S+)")
    for ext in ("*.prefab", "*.unity", "*.asset"):
        for f in ASSETS.rglob(ext):
            try: text = f.read_text(encoding="utf-8", errors="replace")
            except OSError: continue
            if "m_Break" not in text: continue
            for n, line in enumerate(text.splitlines(), 1):
                m = rx.match(line)
                if m and m.group(2) not in ("Infinity", "inf", "+inf"):
                    bad.append(f"{rel(f)}:{n}: joint m_Break{m.group(1)} = {m.group(2)} (must be Infinity, JointBreakPolicy)")
    return bad

def check_code():
    bad = []
    rx = re.compile(r"\.\s*break(Force|Torque)\s*=(?!=)\s*([^;]+);")
    for f in ASSETS.rglob("*.cs"):
        parts = f.relative_to(ASSETS).parts
        if any(p in ("Tests", "Editor") for p in parts): continue
        for n, line in enumerate(f.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
            code = line.split("//", 1)[0]
            for m in rx.finditer(code):
                if "JointBreakPolicy." not in m.group(2):
                    bad.append(f"{rel(f)}:{n}: break{m.group(1)} = {m.group(2).strip()} (use Crashverse.Sim.JointBreakPolicy)")
    return bad

def need(path, pattern, what):
    p = ROOT / path
    if not p.exists(): return [f"{path}: missing ({what})"]
    return [] if re.search(pattern, p.read_text(encoding="utf-8"), re.M) else [f"{path}: expected {what}"]

def check_policy_and_lock():
    pol = "Assets/_Project/Scripts/Physics/Core/JointBreakPolicy.cs"
    qc = "Assets/_Project/Scripts/Perf/QualityController.cs"
    return (need(pol, r"const float BreakForce = float\.PositiveInfinity;", "BreakForce = float.PositiveInfinity")
          + need(pol, r"const float BreakTorque = float\.PositiveInfinity;", "BreakTorque = float.PositiveInfinity")
          + need(pol, r"ExplicitKitValues = AuthoredValueHandling\.Ignored;", "ExplicitKitValues = Ignored")
          + need(pol, r"ResolveBreakForce\(float authored\) => BreakForce;", "ResolveBreakForce always BreakForce")
          + need(pol, r"ResolveBreakTorque\(float authored\) => BreakTorque;", "ResolveBreakTorque always BreakTorque")
          + need(qc, r"const float FixedStep = 0\.02f;", "FixedStep = 0.02f (50 Hz)")
          + need(qc, r"const int SolverIterations = 6;", "SolverIterations = 6")
          + need(qc, r"const float SleepThreshold = 0\.08f;", "SleepThreshold = 0.08f")
          + need("ProjectSettings/TimeManager.asset", r"^\s*Fixed Timestep: 0\.02\s*$", "Fixed Timestep: 0.02")
          + need("Assets/_Project/Scripts/Physics/SimRunner.cs", r"const float Step = SimTime\.Step;", "SimRunner Step = SimTime.Step")
          + need("Assets/_Project/Scripts/Physics/Core/TickInputQueue.cs", r"const int TicksPerSecond = 50;", "SimTime.TicksPerSecond = 50")
          + need("Assets/_Project/Scripts/Physics/Core/TickInputQueue.cs", r"const float Step = 0\.02f;", "SimTime.Step = 0.02f")
          + need("Assets/_Project/Scripts/Destruction/DestructibleBlock.cs", r"_rb\.sleepThreshold = 0\.08f;", "block sleepThreshold 0.08f")
          + need("Assets/_Project/Scripts/Destruction/DestructibleBlock.cs", r"j\.breakForce = Crashverse\.Sim\.JointBreakPolicy\.BreakForce;", "AddSupport uses JointBreakPolicy"))

def main():
    bad = check_serialized() + check_code() + check_policy_and_lock()
    if bad:
        print("JointBreakPolicy / ranked lock check FAILED:"); print("\n".join("  " + b for b in bad)); return 1
    print("JointBreakPolicy / ranked lock check: OK"); return 0

if __name__ == "__main__":
    sys.exit(main())
