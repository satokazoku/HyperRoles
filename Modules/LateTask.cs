using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

//Town Of Hostより　2026/10/25追加

namespace HyperRoles
{
    class LateTask
    {
        public string name;
        public float timer;
        public Action action;
        public bool? NoLog;
        public bool IsStop;
        public bool Isruned;
        public static List<LateTask> Tasks = new();
        public bool Run(float deltaTime)
        {
            timer -= deltaTime;
            if (timer <= 0)
            {
                try
                {
                    Isruned = true;
                    action();
                }
                catch
                {
                }
                return true;
            }
            return false;
        }
        public void CallStop()
        {
            this.IsStop = true;
        }
        public LateTask(Action action, float time, [CallerMemberName] string name = "", bool? NoLog = false)
        {
            this.action = action;
            this.timer = time;
            this.name = name;
            this.NoLog = NoLog;
            this.IsStop = false;
            if (time <= 0)
            {
                try { action(); }
                catch { }
                return;
            }
            Tasks.Add(this);
        }
        public static void Update(float deltaTime)
        {
            var TasksToRemove = new List<LateTask>();
            for (int i = 0; i < Tasks.Count; i++)
            {
                var task = Tasks[i];
                try
                {
                    if (task.Run(deltaTime))
                    {
                        TasksToRemove.Add(task);
                        continue;
                    }
                    if (task.IsStop)
                    {
                        TasksToRemove.Add(task);
                    }
                }
                catch
                {
                    TasksToRemove.Add(task);
                }
            }
            TasksToRemove.ForEach(task => Tasks.Remove(task));
        }
    }
}