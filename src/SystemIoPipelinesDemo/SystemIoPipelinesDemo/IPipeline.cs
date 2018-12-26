using System.IO.Pipelines;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    public interface IPipeline
    {
        Task Read(Pipe pipe, string path);

        Task Write(Pipe pipe, string path);
    }
}
