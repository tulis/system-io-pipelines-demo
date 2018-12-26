using System;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    // https://github.com/StackExchange/StackExchange.Redis/blob/3f7e5466c6bbff96a3ed1130b637a097d21f3fed/src/StackExchange.Redis/LoggingPipe.cs
    // https://blog.marcgravell.com/2018/07/pipe-dreams-part-2.html
    // https://blogs.msdn.microsoft.com/dotnet/2018/07/09/system-io-pipelines-high-performance-io-in-net/
    // https://github.com/davidfowl/TcpEcho/blob/master/src/PipelinesServer/Program.cs
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var program = new Program();
                var cancellationTokenSource = new CancellationTokenSource();
                var streamPipelines = new IStreamPipeline[] {
                    //new ReadFileStreamPipeline(path: $@"blabla.jpg")
                    new ReadHttpClientStreamPipeline(url: $@"https://upload.wikimedia.org/wikipedia/commons/f/fe/01R_Oct_12_2012_0905Z.jpg")
                    , new WriteFileStreamPipeline(path: $@"blabla_copied.jpg")
                };

                var tasks = Task.WhenAll(streamPipelines
                    .Select(streamPipeline => streamPipeline
                        .Stream(program.Pipe, cancellationTokenSource))
                    .ToArray());

                await tasks;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"ERROR - {exception.Message}");
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }

        public Program()
        {
            this.Pipe = new Pipe();

            this.Pipe.Writer.OnReaderCompleted(
                callback: (exception, state) =>
                {
                    if (exception != null)
                    {
                        Console.WriteLine($"ERROR - {exception.Message}");
                    }

                    //(state as PipeReader).Complete(exception);

                    Console.WriteLine($"INFO - {nameof(this.Pipe)}.{nameof(this.Pipe.Reader)} is completed.");
                }, state: this.Pipe.Reader);

            this.Pipe.Reader.OnWriterCompleted(
                callback: (exception, state) =>
                {
                    if (exception != null)
                    {
                        Console.WriteLine($"ERROR - {exception.Message}");
                    }

                    //(state as PipeWriter).Complete(exception);

                    Console.WriteLine($"INFO - {nameof(this.Pipe)}.{nameof(this.Pipe.Writer)} is completed.");
                }, state: this.Pipe.Writer);
        }

        private Pipe Pipe { get; }
    }
}
