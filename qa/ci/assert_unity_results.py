#!/usr/bin/env python3
"""Hard guard for Unity NUnit result XML: fail on missing XML, 0 tests, skipped/inconclusive, or failures.
Usage: assert_unity_results.py <results_dir> [--require-playmode]"""
import glob, sys, xml.etree.ElementTree as ET
d = sys.argv[1] if len(sys.argv) > 1 else "test-results"
need_pm = "--require-playmode" in sys.argv
files = glob.glob(f"{d}/**/*.xml", recursive=True)
if not files: sys.exit("::error::no Unity test result XML produced (crash before tests? check editor log)")
keys = ("total", "passed", "failed", "skipped", "inconclusive"); tot = dict.fromkeys(keys, 0); pm = 0
for f in files:
    r = ET.parse(f).getroot()
    if r.tag != "test-run": continue
    v = {k: int(r.get(k) or 0) for k in keys}; print(f, v)
    for k in keys: tot[k] += v[k]
    if "playmode" in f.lower(): pm += v["total"]
print("TOTAL", tot, "playmode", pm)
if tot["total"] == 0: sys.exit("::error::0 tests ran")
if need_pm and pm == 0: sys.exit("::error::0 PlayMode tests ran")
if tot["skipped"] or tot["inconclusive"]: sys.exit(f"::error::{tot['skipped']} skipped / {tot['inconclusive']} inconclusive tests")
if tot["failed"]: sys.exit(f"::error::{tot['failed']} tests failed")
