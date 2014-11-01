﻿using System;

namespace AgFx
{
    public interface IWorkerThread
    {
        void AddWorkItem(Action action);
        int CurrentlyExecutingTasks { get; }
    }    
}
