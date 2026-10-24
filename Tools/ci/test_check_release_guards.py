#!/usr/bin/env python3
"""Tests for the dev aim hook release guard (source scan + built global-metadata.dat scan on fixtures).
Run: python3 Tools/ci/test_check_release_guards.py"""
import pathlib, sys, tempfile, unittest, zipfile, json
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import check_release_guards as g

class HookGuard(unittest.TestCase):
    def tmp(self, name, text):
        d = pathlib.Path(tempfile.mkdtemp()); p = d / name; p.write_text(text); return p

    def test_repo_source_is_clean(self):
        files = [p for p in (g.ROOT / "Assets").rglob("*.cs") if "Editor" not in p.parts]
        self.assertEqual([b for f in files for b in g.scan(f)], [])
        self.assertEqual(g.check_hook_files(), [])

    def test_unguarded_hook_symbol_fails(self):
        p = self.tmp("A.cs", "class A { void M() { Crashverse.DevHooks.DevAimHook.Attach(null, null); } }\n")
        self.assertTrue(g.scan(p))

    def test_ored_guard_fails(self):
        p = self.tmp("A.cs", "#if CRASHVERSE_DEV || DEVELOPMENT_BUILD\nvar f = \"dev_aim_cmd.json\";\n#endif\n")
        self.assertTrue(g.scan(p))

    def test_bare_guard_passes(self):
        p = self.tmp("A.cs", "#if CRASHVERSE_DEV\nvar f = \"dev_aim_cmd.json\";\n#endif\n")
        self.assertEqual(g.scan(p), [])

    def test_else_branch_fails(self):
        p = self.tmp("A.cs", "#if CRASHVERSE_DEV\n#else\nDevAimHook.Attach();\n#endif\n")
        self.assertTrue(g.scan(p))

    def test_asmdef_and_wrapping_required(self):
        root = pathlib.Path(tempfile.mkdtemp()); d = root.joinpath(*g.HOOK_DIR); d.mkdir(parents=True)
        (d / "X.cs").write_text("namespace X {}\n")
        (d / "Crashverse.DevHooks.asmdef").write_text(json.dumps({"name": "Crashverse.DevHooks", "defineConstraints": []}))
        self.assertEqual(len(g.check_hook_files(root)), 2)
        (d / "X.cs").write_text("#if CRASHVERSE_DEV\nnamespace X {}\n#endif\n")
        (d / "Crashverse.DevHooks.asmdef").write_text(json.dumps({"defineConstraints": ["CRASHVERSE_DEV"]}))
        self.assertEqual(g.check_hook_files(root), [])

    def fixture_aab(self, meta):
        d = pathlib.Path(tempfile.mkdtemp()); p = d / "app.aab"
        with zipfile.ZipFile(p, "w") as z:
            z.writestr("base/assets/bin/Data/Managed/Metadata/global-metadata.dat", meta)
            z.writestr("base/manifest/AndroidManifest.xml", b"x")
        return p

    def test_built_release_clean_passes(self):
        self.assertEqual(g.scan_built(self.fixture_aab(b"\0GameSession\0AimController\0SetAim\0")), [])

    def test_built_with_hook_fails(self):
        for t in ["DevAimHook", "dev_aim_cmd.json", "ReportDevAimResult", "CRASHVERSE_DEV_AIM"]:
            self.assertTrue(g.scan_built(self.fixture_aab(b"\0GameSession\0" + t.encode() + b"\0")), t)

    def test_built_without_metadata_fails(self):
        d = pathlib.Path(tempfile.mkdtemp()); p = d / "x.apk"
        with zipfile.ZipFile(p, "w") as z: z.writestr("lib/arm64-v8a/libil2cpp.so", b"x")
        self.assertTrue(g.scan_built(p))

    def test_debuggable_scans_androidlib_manifests(self):
        root = pathlib.Path(tempfile.mkdtemp()); a = root / "Assets/Plugins/Android"
        (a / "Foo.androidlib").mkdir(parents=True); (a / "Nested/Bar.androidlib").mkdir(parents=True)
        (a / "AndroidManifest.xml").write_text('<manifest><application/></manifest>')
        (a / "Foo.androidlib/AndroidManifest.xml").write_text('<manifest><application/></manifest>')
        (a / "Nested/Bar.androidlib/AndroidManifest.xml").write_text('<manifest><application android:debuggable="true"/></manifest>')
        self.assertEqual(len(g.android_manifests(root)), 3)
        bad = g.check_debuggable(root)
        self.assertEqual(len(bad), 1); self.assertIn("Bar.androidlib", bad[0])
        (a / "AndroidManifest.xml").write_text('<manifest><application android:debuggable = "true"/></manifest>')
        self.assertEqual(len(g.check_debuggable(root)), 2)

    def test_repo_not_debuggable(self):
        self.assertEqual(g.check_debuggable(), [])

if __name__ == "__main__":
    unittest.main()
