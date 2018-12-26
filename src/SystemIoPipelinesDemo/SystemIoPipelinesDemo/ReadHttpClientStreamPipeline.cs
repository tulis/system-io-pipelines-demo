using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    public class ReadHttpClientStreamPipeline : IStreamPipeline
    {
        public ReadHttpClientStreamPipeline(string url)
        {
            if (String.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException(
                    message: $"[{nameof(url)}] is not provided."
                    , paramName: nameof(url));
            }

            this.Url = url;
        }

        public string Url { get; }

        public async Task Stream(Pipe pipe, CancellationTokenSource cancellationTokenSource)
        {
            if (pipe == null)
            {
                cancellationTokenSource.Cancel();
                throw new ArgumentException(
                    message: $"[{nameof(pipe)}] is not provided."
                    , paramName: nameof(pipe));
            }

            using (var httpClient = new HttpClient())
            using(var stream = await httpClient.GetStreamAsync(this.Url))
            {
                while (true)
                {
                    Memory<byte> buffer = pipe.Writer.GetMemory(1);
                    int bytes = await stream.ReadAsync(buffer, cancellationTokenSource.Token);
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
