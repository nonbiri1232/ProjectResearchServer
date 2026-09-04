using MagicOnion;
using MagicOnion.Server;
using MyApp.Shared;

namespace MyApp.Server.Services;

public class MyFirstService : ServiceBase<IMyFirstService>, IMyFirstService
{
    public async UnaryResult<int> SumAsync(int x,int y)
    {
        Console.WriteLine($"Received:{x},{y}");
        return x + y;
    }
}

//http://localhost:5298