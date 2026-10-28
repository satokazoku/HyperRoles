using HarmonyLib;

namespace HyperRoles;

/// <summary>
/// 全体のタスク数(タスクバーの進捗)の数え方を差し替える。
/// バニラは「バニラの役職が、タスクを数える役職か」で決めるので、
/// それに加えて、自前の役職の HasTask が false のプレイヤーを、数えないようにする。
/// </summary>
[HarmonyPatch(typeof(GameData), nameof(GameData.RecomputeTaskCounts))]
static class RecomputeTaskCountsPatch
{
    public static bool Prefix(GameData __instance)
    {
        __instance.TotalTasks = 0;
        __instance.CompletedTasks = 0;

        bool ghostsDoTasks = GameManager.Instance != null
            && GameManager.Instance.LogicOptions.GetGhostsDoTasks();

        for (int i = 0; i < __instance.AllPlayers.Count; i++)
        {
            var info = __instance.AllPlayers[i];

            // ここまではバニラと同じ条件
            if (info == null || info.Disconnected || info.Tasks == null || info.Object == null) continue;
            if (info.IsDead && !ghostsDoTasks) continue;
            if (info.Role == null || !info.Role.TasksCountTowardProgress) continue;

            // 自前の役職が、タスクを持たない(HasTask = false)なら、数えない
            if (CustomRoleManager.TryGetRole(info.Object, out var role) && !role.HasTask) continue;

            for (int j = 0; j < info.Tasks.Count; j++)
            {
                __instance.TotalTasks++;
                if (info.Tasks[j].Complete) __instance.CompletedTasks++;
            }
        }

        return false; // バニラの数え方は実行しない
    }
}
