using System.Collections.Generic;
using HyperRoles.Modules;
using UnityEngine;

namespace HyperRoles.Roles;

public abstract class RoleBase
{
    /// <summary>
    /// 役職名
    /// </summary>
    public abstract string RoleName { get; }
    /// <summary>
    /// イントロで表示される文(「タスクを行う」など)
    /// </summary>
    public abstract string Intro { get; }
    /// <summary>
    /// 役職の色
    /// </summary>
    public abstract Color RoleColor { get; }
    /// <summary>
    /// 陣営
    /// </summary>
    public abstract Team Team { get; }
    /// <summary>
    /// カウント
    /// </summary>
    public virtual Count CountType => Count.Crewmate;
    /// <summary>
    /// 移植元
    /// </summary>
    public virtual From FromMod => From.None;
    /// <summary>
    /// キルボタンが使えるか
    /// </summary>
    public virtual bool CanKill => false;
    /// <summary>
    /// インポスターベントボタンが使えるか
    /// </summary>
    public virtual bool CanVent => false;
    /// <summary>
    /// キルクールダウン(CanKillがtrueの場合のみ使う)
    /// </summary>
    public virtual float KillCoolDown => Main.NormalOptions.KillCooldown;
    /// <summary>
    /// カスタムボタンを有効化する
    /// </summary>
    public virtual bool ShowUseButton => true;
    public static string GetString(string str, Dictionary<string, string> replacementDic = null) => Translator.GetString(str, replacementDic);
    /// <summary>
    /// インスタンス作成後すぐ呼ばれる
    /// </summary>
    public virtual void Add()
    { }
    /// <summary>
    /// インスタンスが破棄される前に呼ばれる
    /// </summary>
    public virtual void OnDestroy()
    { }
    /// <summary>
    /// 毎フレーム呼ばれる関数
    /// 本人確認不要
    /// </summary>
    /// <param name="player"></param>
    public virtual void OnFixedUpdate(PlayerControl player)
    { }
    /// <summary>
    /// 役職名の横に表示される文字(シェリフ(Progress)のように表示)
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    public virtual string ProgressText(PlayerControl player) => "";
    /// <summary>
    /// MurderPlayer処理
    /// 本人確認必須
    /// </summary>
    /// <param name="killer">キラー</param>
    /// <param name="target">ターゲット</param>
    /// <param name="player">処理が実行されるプレイヤー</param>
    public virtual void OnMurderPlayer(PlayerControl killer, PlayerControl target, PlayerControl player)
    { }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="killer"></param>
    /// <param name="target"></param>
    /// <returns>falseを返すとキルが無効化される</returns>
    public virtual bool OnCheckMurder(PlayerControl killer, PlayerControl target) => true;
}

public enum Team
{
    none = 0,
    Impostor,
    Crewmate,
    Neutral,
}
public enum Count
{
    none = 0,
    Impostor,
    Crewmate,
    Jackal
}
public enum From
{
    None,
    TownOfHost_Pko,
    SheriffMod,
    TheOtherRoles
}