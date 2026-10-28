using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HyperRoles.Roles;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using RoleId = HyperRoles.CustomRoleManager.RoleId;

namespace HyperRoles;

/// <summary>
/// ロビーの「ゲーム設定」画面に、「HyperRolesの設定」タブを追加する。
/// バニラの「ロールの設定」ボタンの下にボタンを出し、押すと、独自のパネルが開く。
///
/// 数値の編集(− 値 +)と、役職名の後ろの四角は、バニラの「ロールの設定」の行を複製して、そのまま使う。
///
/// 役職の一覧:  [役職名(役職の色の四角)]  [− 最大数 +]  [− 出現率 +]  [設定]
/// 設定の画面:  [名前(役職の色の四角)]    [− 値 +]  … RoleBase.Options の項目ごとに1行
/// </summary>
public static class ModRoleSettingsMenu
{
    const string TabLabel = "HyperRolesの設定";
    const int RolesPerPage = 5;
    const int MaxCountLimit = 15;

    /// <summary>出現率の、+/− 1回あたりの増減(%)</summary>
    const int SpawnRateStep = 10;

    // 位置の調整用。パネルの原点は、バニラの「ロールの設定」の中身の中心。
    // 実測: 画面の中身は、x が約 -3.3 〜 +3.3、y が約 -1.6 〜 +2.7 の範囲。
    const float TitleY = 2.35f;          // タイトル、前へ/次へ/戻る のボタンの高さ
    const float FirstRowY = 1.35f;       // 1行目の高さ
    const float RowHeight = 0.62f;       // 行の間隔
    const float RowLeftX = -3.25f;       // 行(役職名の四角)の左端
    const float RowMaxWidth = 4.9f;      // 行の最大の幅(これより広いと、縮小する)
    const float SettingButtonX = 2.7f;   // 「設定」ボタンの位置
    const float SideButtonX = 2.7f;      // 「前へ」「次へ」「戻る」の位置(左右)

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("ModRoleSettings");

    // 画面ごとの状態(設定画面は、開くたびに作り直されるので、そのたびに初期化する)
    static GameSettingMenu menu;
    static GameObject panel;
    static PassiveButton tabButton;
    static int page;
    static RoleId? detailRole;
    static bool modTabActive;                       // こちらのタブが、選ばれているか
    static bool loggedRowParts;
    static readonly List<GameObject> items = new(); // 今表示している部品(表示を作り直すたびに消す)

    /// <summary>バニラの行を複製して取り出した、部品の一式</summary>
    sealed class RoleRow
    {
        public GameObject Root;
        public TextMeshPro Title;            // 役職名
        public SpriteRenderer Label;         // 役職名の後ろの四角(役職の色にする)
        public PassiveButton CountMinus, CountPlus;     // 左の数値(最大数 / 設定の値)
        public PassiveButton ChanceMinus, ChancePlus;   // 右の数値(出現率)
        public TextMeshPro CountText, ChanceText;
    }

    // ------------------------------------------------------------
    // 画面の組み立て
    // ------------------------------------------------------------

    internal static void Build(GameSettingMenu m)
    {
        menu = m;
        panel = null;
        tabButton = null;
        page = 0;
        detailRole = null;
        modTabActive = false;
        items.Clear();

        var roleButton = m.RoleSettingsButton;
        var gameButton = m.GameSettingsButton;
        if (roleButton == null || gameButton == null || m.MenuDescriptionText == null || m.RoleSettingsTab == null)
        {
            log.LogWarning("[ModRoleSettings] template objects not found");
            return;
        }

        // 1. パネル(最初は非表示)。バニラの「ロールの設定」の中身と、同じ親・同じ位置に置く
        panel = new GameObject("HyperRolesPanel");
        panel.transform.SetParent(m.RoleSettingsTab.transform.parent, false);
        panel.transform.localPosition = m.RoleSettingsTab.transform.localPosition;
        panel.SetActive(false);

        // 2. 「ロールの設定」ボタンの下に、タブのボタンを出す(上のボタンとの間隔と同じだけ下げる)
        float step = gameButton.transform.localPosition.y - roleButton.transform.localPosition.y;
        tabButton = Object.Instantiate(roleButton, roleButton.transform.parent);
        tabButton.name = "HyperRolesTabButton";
        tabButton.transform.localPosition = roleButton.transform.localPosition + new Vector3(0f, -step, 0f);
        SetButtonText(tabButton, TabLabel);
        SetClick(tabButton, ShowPanel);

        log.LogInfo("[ModRoleSettings] built");
    }

    /// <summary>
    /// バニラの ChangeTab のあとに呼ぶ。
    /// ・バニラは、タブのボタンにカーソルを合わせただけでも ChangeTab(previewOnly = true) を呼んで、そのタブの中身を出す。
    ///   こちらのタブが選ばれている間は、その表示を、すぐに打ち消して、こちらのパネルを出したままにする。
    /// ・バニラのタブが、実際に選ばれたとき(previewOnly = false)は、こちらのパネルを閉じる。
    /// </summary>
    internal static void OnChangeTab(bool previewOnly)
    {
        if (panel == null) return;

        if (previewOnly)
        {
            if (modTabActive) ShowOnlyPanel();
            return;
        }

        modTabActive = false;
        panel.SetActive(false);
    }

    static void ShowPanel()
    {
        if (menu == null || panel == null) return;

        modTabActive = true;
        ShowOnlyPanel();
        menu.MenuDescriptionText.text = "HyperRolesの役職の設定を編集する。";
        Refresh();
    }

    /// <summary>バニラの3つのタブの中身を隠して、こちらのパネルだけを出す</summary>
    static void ShowOnlyPanel()
    {
        menu.PresetsTab.gameObject.SetActive(false);
        menu.GameSettingsTab.gameObject.SetActive(false);
        menu.RoleSettingsTab.gameObject.SetActive(false);
        panel.SetActive(true);
    }

    // ------------------------------------------------------------
    // 表示の作り直し
    // ------------------------------------------------------------

    static void Refresh()
    {
        foreach (var item in items)
        {
            if (item != null) Object.Destroy(item);
        }
        items.Clear();

        if (detailRole.HasValue) BuildDetail(detailRole.Value);
        else BuildRoleList();
    }

    /// <summary>役職の一覧</summary>
    static void BuildRoleList()
    {
        var roles = CustomRoleManager.AssignableRoles().ToList();
        int pages = Math.Max(1, (roles.Count + RolesPerPage - 1) / RolesPerPage);
        page = Mathf.Clamp(page, 0, pages - 1);

        MakeText($"{TabLabel}  ({page + 1}/{pages})", new Vector3(0f, TitleY, 0f), 2.6f, TextAlignmentOptions.Center);

        // ページの切り替え(タイトルの左右)
        MakeButton("前へ", new Vector3(-SideButtonX, TitleY, 0f), 0.3f, () =>
        {
            page--;
            Refresh();
        });
        MakeButton("次へ", new Vector3(SideButtonX, TitleY, 0f), 0.3f, () =>
        {
            page++;
            Refresh();
        });

        bool headerMade = false;
        for (int i = 0; i < RolesPerPage; i++)
        {
            int index = page * RolesPerPage + i;
            if (index >= roles.Count) break;

            var id = roles[index];
            var template = CustomRoleManager.CreateRole(id);
            if (template == null) continue;

            float y = FirstRowY - i * RowHeight;

            // 役職名(後ろの四角は、役職の色)と、2つの数値(最大数・出現率)は、バニラの行をそのまま使う
            var row = CreateRow(template.RoleName, template.RoleColor);
            if (row == null) continue;

            row.CountText.text = CustomRoleManager.GetMaxCount(id).ToString();
            SetClick(row.CountMinus, () => ChangeMaxCount(id, -1));
            SetClick(row.CountPlus, () => ChangeMaxCount(id, +1));

            row.ChanceText.text = RoleSettings.SpawnRate(id).ToString();
            SetClick(row.ChanceMinus, () => ChangeSpawnRate(id, -1));
            SetClick(row.ChancePlus, () => ChangeSpawnRate(id, +1));

            PlaceRow(row, y);

            // その役職の設定の画面へ
            MakeButton("設定", new Vector3(SettingButtonX, y, 0f), 0.3f, () =>
            {
                detailRole = id;
                Refresh();
            });

            // 列の見出し(1行目の数値の位置に合わせて、上に出す)
            if (!headerMade)
            {
                headerMade = true;
                var countPos = panel.transform.InverseTransformPoint(row.CountText.transform.position);
                var chancePos = panel.transform.InverseTransformPoint(row.ChanceText.transform.position);
                MakeText("最大数", new Vector3(countPos.x, y + 0.45f, 0f), 1.6f, TextAlignmentOptions.Center);
                MakeText("出現率(%)", new Vector3(chancePos.x, y + 0.45f, 0f), 1.6f, TextAlignmentOptions.Center);
            }
        }
    }

    /// <summary>1つの役職の、役職ごとの設定(RoleBase.Options)</summary>
    static void BuildDetail(RoleId id)
    {
        var template = CustomRoleManager.CreateRole(id);
        if (template == null)
        {
            detailRole = null;
            BuildRoleList();
            return;
        }
        var setting = RoleSettings.Get(id);

        MakeText(template.RoleName, new Vector3(0f, TitleY, 0f), 2.6f, TextAlignmentOptions.Center, template.RoleColor);
        MakeButton("戻る", new Vector3(-SideButtonX, TitleY, 0f), 0.3f, () =>
        {
            detailRole = null;
            Refresh();
        });

        int rowIndex = 0;

        // 「名前 [− 値 +]」の1行(バニラの行の、右の数値は隠す)
        void AddRow(string name, string valueText, Action<int> change)
        {
            float y = FirstRowY - rowIndex * RowHeight;
            rowIndex++;

            var row = CreateRow(name, template.RoleColor);
            if (row == null) return;

            HideChance(row);
            row.CountText.text = valueText;
            SetClick(row.CountMinus, () => Edit(change, -1));
            SetClick(row.CountPlus, () => Edit(change, +1));
            PlaceRow(row, y);
        }

        // 役職ごとの設定(オプションID 2以降。RoleBase.Options の Dictionary)。間隔は、項目ごとの Step
        foreach (var pair in template.Options.Where(p => p.Key >= 2).OrderBy(p => p.Key))
        {
            int optionId = pair.Key;
            var def = pair.Value;
            float current = setting.Values.TryGetValue(optionId, out var v) ? v : def.Default;

            AddRow(def.name(), current.ToString("0.##"), direction =>
            {
                var now = setting.Values.TryGetValue(optionId, out var value) ? value : def.Default;
                setting.Values[optionId] = Mathf.Clamp(now + direction * def.ResolvedStep, def.Min, def.Max);
            });
        }

        if (rowIndex == 0)
        {
            MakeText("この役職には、設定する項目がありません", new Vector3(0f, FirstRowY, 0f), 2f, TextAlignmentOptions.Center);
        }
    }

    // ------------------------------------------------------------
    // バニラの行を複製して使う
    // ------------------------------------------------------------

    /// <summary>
    /// バニラの「ロールの設定」の行(役職名の四角と、2つの数値の+/−)を複製して、部品を取り出す。
    /// 部品の名前が分からなくても動くよう、左右の位置で見分ける(左から、役職名・左の数値・右の数値)。
    /// 位置と大きさは、PlaceRow で決める(ここでは、決めない)。
    /// </summary>
    static RoleRow CreateRow(string title, Color color)
    {
        var origin = menu.RoleSettingsTab.roleOptionSettingOrigin;
        if (origin == null)
        {
            log.LogWarning("[ModRoleSettings] roleOptionSettingOrigin not found");
            return null;
        }

        var clone = Object.Instantiate(origin, panel.transform);
        var root = clone.gameObject;
        root.SetActive(true);
        items.Add(root);

        // バニラの行の動き(役職の情報を読み込む処理など)は止めて、見た目の部品だけを使う
        clone.enabled = false;
        Object.Destroy(clone);

        // ボタンは、右から4つ(左の数値の −と+、右の数値の −と+)を使う
        var buttons = root.GetComponentsInChildren<PassiveButton>(true)
            .OrderBy(b => b.transform.position.x)
            .ToArray();
        var texts = root.GetComponentsInChildren<TextMeshPro>(true).ToArray();
        var sprites = root.GetComponentsInChildren<SpriteRenderer>(true).ToArray();

        if (!loggedRowParts)
        {
            loggedRowParts = true;
            log.LogInfo($"[ModRoleSettings] row parts: buttons={buttons.Length}, texts={texts.Length}, sprites={sprites.Length}");
        }

        if (buttons.Length < 4 || texts.Length < 3)
        {
            log.LogWarning($"[ModRoleSettings] unexpected row parts: buttons={buttons.Length}, texts={texts.Length}, sprites={sprites.Length}");
            return null;
        }
        buttons = buttons.Skip(buttons.Length - 4).ToArray();

        var row = new RoleRow
        {
            Root = root,
            CountMinus = buttons[0],
            CountPlus = buttons[1],
            ChanceMinus = buttons[2],
            ChancePlus = buttons[3],
        };

        // 役職名: いちばん左の文字
        row.Title = texts.OrderBy(t => t.transform.position.x).First();

        // 数値の文字: 「−」と「+」の真ん中に、いちばん近い文字
        row.CountText = NearestText(texts, row.Title, MidX(row.CountMinus, row.CountPlus));
        row.ChanceText = NearestText(texts, row.Title, MidX(row.ChanceMinus, row.ChancePlus));

        // 役職名の後ろの四角: ボタンより左にある絵のうち、いちばん幅が広いもの
        float limitX = row.CountMinus.transform.position.x;
        row.Label = sprites
            .Where(s => s.transform.position.x < limitX)
            .OrderByDescending(s => s.bounds.size.x)
            .FirstOrDefault();

        // バニラの行は、スクロールの枠(マスク)の中でだけ見えるように作られていて、枠の外に置くと、何も見えなくなる。
        // 絵と文字の材質を、マスクの設定が入っていない、普通のものに差し替える。
        MakeVisibleOutsideScroller(root, buttons);

        RemoveTranslator(row.Title);
        RemoveTranslator(row.CountText);
        RemoveTranslator(row.ChanceText);
        row.Title.text = title;
        if (row.Label != null) row.Label.color = color; // 役職の色の四角にする

        // 数値の+/−を、こちらの処理だけにする(押したときの処理は、あとで SetClick で入れる)
        foreach (var b in buttons)
        {
            b.OnMouseOver = new();
            b.OnMouseOut = new();
        }

        return row;
    }

    /// <summary>枠(マスク)の外でも、絵と文字が見えるようにする</summary>
    static void MakeVisibleOutsideScroller(GameObject root, PassiveButton[] buttons)
    {
        // 普通に見えているタブのボタンの絵の材質を、見本として使う
        var sample = menu.RoleSettingsButton.GetComponentInChildren<SpriteRenderer>(true);
        var plainMaterial = sample != null ? sample.sharedMaterial : null;

        foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sprite.maskInteraction = SpriteMaskInteraction.None;
            if (plainMaterial != null) sprite.sharedMaterial = plainMaterial;
        }

        // 各テキストは、自身のフォントアセットに対応するマテリアルを使用しつつ、マスクの影響を受けないようにする
        foreach (var text in root.GetComponentsInChildren<TextMeshPro>(true))
        {
            if (text.font != null)
            {
                text.fontSharedMaterial = text.font.material;
            }
            // スクロールマスクのステンシルテストを無効化
            if (text.fontSharedMaterial != null)
            {
                text.fontSharedMaterial.SetFloat("_StencilComp", 0);
            }
        }

        // ボタンは、スクロールの枠の外のクリックを無視する設定が入っているので、外す
        foreach (var button in buttons)
        {
            button.ClickMask = null;
        }
    }

    /// <summary>
    /// 行の大きさと位置を決める。行の左端を RowLeftX に合わせ、幅が RowMaxWidth より広ければ、縮小する。
    /// (表示されている絵の、左端と右端から、幅を測る)
    /// </summary>
    static void PlaceRow(RoleRow row, float y)
    {
        var rootTransform = row.Root.transform;
        rootTransform.localScale = Vector3.one;
        rootTransform.localPosition = new Vector3(0f, y, 0f);

        if (!TryGetBounds(row.Root, out var min, out var max)) return;

        float width = panel.transform.InverseTransformPoint(max).x - panel.transform.InverseTransformPoint(min).x;
        float scale = Mathf.Clamp(RowMaxWidth / Mathf.Max(width, 0.01f), 0.2f, 1f);
        rootTransform.localScale = Vector3.one * scale;

        if (!TryGetBounds(row.Root, out min, out max)) return;
        float left = panel.transform.InverseTransformPoint(min).x;
        rootTransform.localPosition += new Vector3(RowLeftX - left, 0f, 0f);
    }

    /// <summary>表示されている絵(SpriteRenderer)の、全体の範囲(ワールド座標)</summary>
    static bool TryGetBounds(GameObject root, out Vector3 min, out Vector3 max)
    {
        min = max = Vector3.zero;
        bool any = false;
        foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (!sprite.enabled) continue;
            var b = sprite.bounds;
            if (!any)
            {
                min = b.min;
                max = b.max;
                any = true;
            }
            else
            {
                min = Vector3.Min(min, b.min);
                max = Vector3.Max(max, b.max);
            }
        }
        return any;
    }

    static float MidX(PassiveButton a, PassiveButton b)
        => (a.transform.position.x + b.transform.position.x) * 0.5f;

    static TextMeshPro NearestText(TextMeshPro[] texts, TextMeshPro exclude, float x)
        => texts.Where(t => t != exclude).OrderBy(t => Mathf.Abs(t.transform.position.x - x)).First();

    /// <summary>右の数値(出現率)の部分を隠す。数値が1つだけの行にするため</summary>
    static void HideChance(RoleRow row)
    {
        var group = row.ChanceMinus.transform.parent;
        if (group != null && group != row.Root.transform && group == row.ChancePlus.transform.parent)
        {
            group.gameObject.SetActive(false);
            return;
        }
        row.ChanceMinus.gameObject.SetActive(false);
        row.ChancePlus.gameObject.SetActive(false);
        row.ChanceText.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------
    // 値の変更(ホストだけ)
    // ------------------------------------------------------------

    static void Edit(Action<int> change, int direction)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        change(direction);
        Refresh();
    }

    static void ChangeMaxCount(RoleId id, int direction)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        int next = Mathf.Clamp(CustomRoleManager.GetMaxCount(id) + direction, 0, MaxCountLimit);
        CustomRoleManager.SetMaxCount(id, next);
        Refresh();
    }

    static void ChangeSpawnRate(RoleId id, int direction)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var setting = RoleSettings.Get(id);
        setting.SpawnRate = Mathf.Clamp(setting.SpawnRate + direction * SpawnRateStep, 0, 100);
        Refresh();
    }

    // ------------------------------------------------------------
    // 部品を作る補助関数
    // ------------------------------------------------------------

    /// <summary>文字を1つ作る(バニラの説明文の文字を複製して使う)</summary>
    static TextMeshPro MakeText(string text, Vector3 position, float size, TextAlignmentOptions alignment, Color? color = null)
    {
        var t = Object.Instantiate(menu.MenuDescriptionText, panel.transform);
        RemoveTranslator(t);
        t.gameObject.SetActive(true);
        t.enableAutoSizing = false;
        t.enableWordWrapping = false;
        t.fontSize = size;
        t.alignment = alignment;
        t.color = color ?? Color.white;
        t.text = text;
        t.transform.localPosition = position;
        items.Add(t.gameObject);
        return t;
    }

    /// <summary>ボタンを1つ作る(バニラのタブのボタンを複製して、小さくして使う)</summary>
    static PassiveButton MakeButton(string label, Vector3 position, float scale, Action onClick)
    {
        var b = Object.Instantiate(menu.RoleSettingsButton, panel.transform);
        b.name = "HyperRolesButton_" + label;
        b.transform.localPosition = position;
        b.transform.localScale = Vector3.one * scale;
        SetButtonText(b, label);
        SetClick(b, onClick);
        items.Add(b.gameObject);
        return b;
    }

    static void SetButtonText(PassiveButton button, string text)
    {
        var tmp = button.GetComponentInChildren<TextMeshPro>(true);
        if (tmp == null) return;
        RemoveTranslator(tmp);
        tmp.text = text;
    }

    /// <summary>
    /// 押したときの処理を、自前のものだけにする(元の処理は外す)。
    /// カーソルを合わせたとき/外したときの処理も外す。複製元のタブのボタンには、
    /// 「カーソルを合わせると、バニラのそのタブの中身を、先に見せる」処理が入っているため。
    /// </summary>
    static void SetClick(PassiveButton button, Action action)
    {
        button.OnClick = new();
        button.OnMouseOver = new();
        button.OnMouseOut = new();
        button.OnClick.AddListener((Action)action);
    }

    /// <summary>自動翻訳が、文字を書き戻さないよう、翻訳の部品を外す</summary>
    static void RemoveTranslator(TMP_Text text)
    {
        var translator = text.GetComponent<TextTranslatorTMP>();
        if (translator != null) Object.Destroy(translator);
    }
}

[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
static class ModRoleSettingsStartPatch
{
    public static void Postfix(GameSettingMenu __instance)
    {
        try
        {
            ModRoleSettingsMenu.Build(__instance);
        }
        catch (Exception e)
        {
            BepInEx.Logging.Logger.CreateLogSource("ModRoleSettings").LogError(e);
        }
    }
}

[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.ChangeTab))]
static class ModRoleSettingsChangeTabPatch
{
    // 2つ目の引数(previewOnly)は、名前がバージョンで違っても動くよう、位置で受ける
    public static void Postfix([HarmonyArgument(1)] bool previewOnly)
        => ModRoleSettingsMenu.OnChangeTab(previewOnly);
}