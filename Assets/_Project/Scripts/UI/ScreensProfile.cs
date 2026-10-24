using System;
using Crashverse.Core;
using Crashverse.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Crashverse.UI
{
    // ───────────────────────────── Target UI §1: Login & Sign Up ─────────────────────────────
    /// <summary>
    /// First run after the age gate. The server stays offline in this build, so every social / email sign-in button is shown
    /// disabled with "Coming soon"; "Play as Guest" creates a local guest profile (next screen).
    /// </summary>
    public class LoginScreen : UIScreen
    {
        public override bool OnBack() => false; // root: router asks to quit
        protected override void Build(RectTransform root)
        {
            UIArt.Background(root, "bg_season_sunny_shores", Color.white);
            var dim = UIFactory.Panel("Dim", root, new Color(0.02f, 0.05f, 0.15f, 0.35f)); UIFactory.Stretch(dim.rectTransform); dim.raycastTarget = false;
            var v = V2.Split(root, 150f, out var foot, 20f);
            V2.Title(v, "CRASHVERSE", 44f, Th.goldFill, Th).GetComponent<LayoutElement>().minHeight = 64f;
            V2.Title(v, "BUILD, AIM, SMASH, CONQUER.", Th.bodySp, Color.white, Th);
            UIFactory.Spacer(v, 24f);
            foreach (var prov in ProfileRules.SocialProviders)
            {
                var b = V2.Glossy(v, (prov == "Email" ? "Create with Email" : "Continue with " + prov) + "  ·  Coming soon", ProviderColor(prov), null, Th, 48f, 0f, Th.bodySp, "Button_" + prov);
                b.interactable = ProfileRules.SocialSignInEnabled(prov);
            }
            V2.Glossy(foot, "Play as Guest", Th.green, () => Router.Push(new ProfileScreen()), Th, 60f, 0f, Th.headingSp, "Button_Guest");
            var note = UIFactory.Label(foot, "Online sign-in is coming soon. Your progress is saved on this device.", Th.captionSp, Color.white, TextAnchor.MiddleCenter, Th);
            note.GetComponent<LayoutElement>().minHeight = 36f; UIArt.Pop(note, V2.Ink, 1f);
        }
        static Color ProviderColor(string p)
        {
            switch (p)
            {
                case "Google": return UITheme.H("4285F4");
                case "Facebook": return UITheme.H("1877F2");
                case "Instagram": return UITheme.H("C13584");
                case "X": return UITheme.H("111111");
                default: return UITheme.H("5B6472");
            }
        }
    }

    // ───────────────────────────── Target UI §2: Create Profile ─────────────────────────────
    /// <summary>Local guest profile: one of 4 avatars, a name (3–16 letters/numbers/space/_), one of 3 badges.</summary>
    public class ProfileScreen : UIScreen
    {
        int _avatar, _badge; InputField _name; Text _err; readonly Button[] _av = new Button[4], _bd = new Button[3];
        readonly bool _editing;
        public ProfileScreen(bool editing = false) { _editing = editing; }
        public string NameText { get => _name != null ? _name.text : ""; set { if (_name != null) _name.text = value; } }
        public string Error => _err != null ? _err.text : "";

        protected override void Build(RectTransform root)
        {
            var meta = AppServices.Content.Meta; var pr = AppServices.Progress.Data.profile;
            _avatar = pr.created ? pr.avatar : 0; _badge = pr.created ? pr.badge : 0;
            UIArt.Background(root, "bg_season_sunny_shores", new Color(0.6f, 0.65f, 0.8f, 1f));
            var card = V2.Card("Profile", root, Th); UIFactory.Stretch(card.rectTransform, 14f);
            var v = V2.Split(card.transform, _editing ? 128f : 80f, out var foot, 14f);
            V2.Title(v, _editing ? "Your Hero" : "Create Your Hero", Th.titleSp, Th.goldFill, Th);
            UIFactory.Label(v, "Choose Avatar", Th.bodySp, Color.white, TextAnchor.MiddleCenter, Th, true);
            var ar = UIFactory.HStack(v, Th.gap, 68f);
            for (int i = 0; i < 4; i++)
            {
                int idx = i; var b = V2.RoundButton(ar, "", Th.navy500, () => { _avatar = idx; Refresh(); }, Th, 64f, "Button_Avatar" + (i + 1));
                var ic = UIArt.Icon(b.transform, meta.avatars.Length > i ? meta.avatars[i] : "avatar_0" + (i + 1), 58f); UIFactory.Stretch(ic.rectTransform, 4f);
                _av[i] = b;
            }
            UIFactory.Label(v, "Player Name", Th.bodySp, Color.white, TextAnchor.MiddleCenter, Th, true);
            _name = NameField(v, pr.created ? pr.name : ProfileRules.DefaultName(Environment.TickCount));
            UIFactory.Label(v, "Choose Your Badge", Th.bodySp, Color.white, TextAnchor.MiddleCenter, Th, true);
            var br = UIFactory.HStack(v, Th.gap * 2, 76f);
            for (int i = 0; i < 3; i++)
            {
                int idx = i; var b = V2.Glossy(br, "", Th.navy500, () => { _badge = idx; Refresh(); }, Th, 72f, 72f, 0f, "Button_Badge" + (i + 1));
                var ic = UIArt.Icon(b.transform, meta.badges.Length > i ? meta.badges[i] : "badge_01_bronze", 64f); UIFactory.Stretch(ic.rectTransform, 4f);
                _bd[i] = b;
            }
            _err = UIFactory.Label(v, "", Th.captionSp, Th.red, TextAnchor.MiddleCenter, Th);
            V2.Glossy(foot, _editing ? "Save" : "Continue", Th.green, Continue, Th, 56f, 0f, Th.headingSp, "Button_Continue");
            if (_editing) UIFactory.Button(foot, "Back", Th.navy500, () => Router.Pop(), Th, 48);
            Refresh();
        }

        InputField NameField(Transform parent, string initial)
        {
            var bg = V2.RoundPanel("NameField", parent, Color.white, true);
            var le = bg.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 48f;
            var input = bg.gameObject.AddComponent<InputField>();
            var txt = UIFactory.Label(bg.transform, "", Th.headingSp, V2.Ink, TextAnchor.MiddleCenter, Th, true); txt.supportRichText = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow; UIFactory.Stretch(txt.rectTransform, 8f);
            var ph = UIFactory.Label(bg.transform, "Your name", Th.headingSp, new Color(0.3f, 0.35f, 0.45f, 0.6f), TextAnchor.MiddleCenter, Th); UIFactory.Stretch(ph.rectTransform, 8f);
            input.textComponent = txt; input.placeholder = ph; input.characterLimit = ProfileRules.MaxName; input.lineType = InputField.LineType.SingleLine;
            input.targetGraphic = bg; input.text = initial;
            input.onEndEdit.AddListener(_ => Validate());
            return input;
        }

        void Refresh()
        {
            for (int i = 0; i < 4; i++) _av[i].image.color = i == _avatar ? Th.goldTrim : Th.navy500;
            for (int i = 0; i < 3; i++) _bd[i].image.color = i == _badge ? Th.goldTrim : Th.navy500;
            Validate();
        }
        void Validate() { _err.text = ProfileRules.ValidateName(ProfileRules.Clean(_name.text)) ?? ""; }

        void Continue()
        {
            var m = AppServices.Content.Meta;
            var err = AppServices.Progress.CreateProfile(_name.text, _avatar, _badge, Mathf.Max(4, m.avatars.Length), Mathf.Max(3, m.badges.Length));
            if (err != null) { _err.text = err; Router.Toast(err); return; }
            if (_editing) { Router.Pop(); return; }
            GameplayDirector.ShowMap();
        }
    }
}
