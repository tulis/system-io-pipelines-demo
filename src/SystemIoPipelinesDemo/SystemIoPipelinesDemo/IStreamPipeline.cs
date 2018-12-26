using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    public interface IStreamPipeline
    {
        Task Stream(Pipe pipe, CancellationTokenSource cancellationTokenSource);
    }
}
