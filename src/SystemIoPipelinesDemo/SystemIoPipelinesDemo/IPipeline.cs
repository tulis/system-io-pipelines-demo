using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    public interface IPipeline
    {
        Task Read(Pipe pipe, string path, CancellationTokenSource cancellationTokenSource);

        Task Write(Pipe pipe, string path, CancellationTokenSource cancellationTokenSource);
    }
}
