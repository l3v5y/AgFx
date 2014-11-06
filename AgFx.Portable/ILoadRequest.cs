using System;
using System.Threading.Tasks;

namespace AgFx
{
    interface ILoadRequest : ILoadContextItem
    {
        Task<LoadRequestResult> Execute();
    }
}
