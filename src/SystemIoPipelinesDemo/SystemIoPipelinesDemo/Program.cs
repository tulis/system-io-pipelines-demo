using System;
using System.IO.Pipelines;
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
            var program = new Program();
            var fileStreamPipeline = new FileStreamPipeline();
            var readFileTask = fileStreamPipeline.Read(program.Pipe, path: $@"blabla.jpg");
            var writeFileTask = fileStreamPipeline.Write(program.Pipe, path: $@"blabla_copied.jpg");

            await Task.WhenAll(readFileTask, writeFileTask);

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
