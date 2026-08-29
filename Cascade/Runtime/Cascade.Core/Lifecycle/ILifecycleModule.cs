using System.Threading;
using Cysharp.Threading.Tasks;

namespace Cascade.Core
{
    public interface ILifecycleModule
    {
        string Name { get; }
        UniTask InitializeAsync(CancellationToken cancellationToken = default);
        void Start();
        void Stop();
        void Reset();
    }
}
