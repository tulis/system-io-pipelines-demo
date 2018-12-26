using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    public class ReadFileStreamPipeline : IStreamPipeline
    {
        public ReadFileStreamPipeline(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    message: $"[{nameof(path)}] is not provided."
                    , paramName: nameof(path));
            }

            this.Path = path;
        }

        public string Path { get; }

        /// <summary>
        /// Read file from file system using <see cref="FileStream"/>
        /// </summary>
        /// <param name="pipe"></param>
        /// <param name="cancellationTokenSource"></param>
        /// <returns></returns>
        public async Task Stream(Pipe pipe, CancellationTokenSource cancellationTokenSource)
        {
            if(pipe == null)
            {
                cancellationTokenSource.Cancel();
                throw new ArgumentException(
                    message: $"[{nameof(pipe)}] is not provided."
                    , paramName: nameof(pipe));
            }

            using (var fileStream = new FileStream(this.Path, FileMode.Open, FileAccess.Read))
            {
                while (true)
                {
                    Memory<byte> buffer = pipe.Writer.GetMemory(1);
                    int bytes = await fileStream.ReadAsync(buffer, cancellationTokenSource.Token);
                    pipe.Writer.Advance(bytes);

                    if (bytes == 0)
                    {
                        // source EOF
                        break;
                    }

                    var flush = await pipe.Writer.FlushAsync(cancellationTokenSource.Token);
                    if (flush.IsCompleted || flush.IsCanceled)
                    {
                        break;
                    }
                }

                pipe.Writer.Complete();
            }
        }
    }
}
