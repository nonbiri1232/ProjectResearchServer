using System;
using MagicOnion;

namespace MyApp.Shared
{
    public interface IMyFirstService : IService<IMyFirstService>
    {
        UnaryResult<int> SumAsync(int x, int y);
    }
}
