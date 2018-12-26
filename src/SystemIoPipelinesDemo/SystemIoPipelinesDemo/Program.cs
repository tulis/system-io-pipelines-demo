using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    // https://github.com/StackExchange/StackExchange.Redis/blob/3f7e5466c6bbff96a3ed1130b637a097d21f3fed/src/StackExchange.Redis/LoggingPipe.cs
    // https://blog.marcgravell.com/2018/07/pipe-dreams-part-2.html
    // https://blogs.msdn.microsoft.com/dotnet/2018/07/09/system-io-pipelines-high-performance-io-in-net/
    // https://github.com/davidfowl/TcpEcho/blob/master/src/PipelinesServer/Program.cs
    class Program : IDuplexPipe
    {
        static async Task Main(string[] args)
        {
            var program = new Program();
            var readFileTask = program.ReadFile(filePath: $@"bla.jpg");
            var copyToS3Task = program.CopyToS3(s3Path: $@"bla-copied.jpg");

            await Task.WhenAll(readFileTask, copyToS3Task);
            //await Task.WhenAll(readFileTask);

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }

        public Program()
        {
            this.Pipe = new Pipe();
            this.ReadPipe = new Pipe();
            this.WritePipe = new Pipe();

            this.Pipe.Writer.OnReaderCompleted(
                callback: (exception, state) =>
                {
                    if (exception != null)
                    {
                        Console.WriteLine($"ERROR - {exception.Message}");
                    }

                    (state as PipeReader).Complete(exception);
                }, state: this.Pipe.Reader);

            this.Pipe.Reader.OnWriterCompleted(
                callback: (exception, state) =>
                {
                    if (exception != null)
                    {
                        Console.WriteLine($"ERROR - {exception.Message}");
                    }

                    (state as PipeWriter).Complete(exception);
                }, state: this.Pipe.Writer);

            //this.WritePipe.Writer.OnReaderCompleted(
            //    callback: (exception, state) =>
            //    {
            //        if (exception != null)
            //        {
            //            Console.WriteLine($"ERROR - {exception.Message}");
            //        }

            //        (state as PipeReader).Complete(exception);
            //    }, state: this.ReadPipe.Reader);

            //this.ReadPipe.Reader.OnWriterCompleted(
            //    callback: (exception, state) =>
            //    {
            //        if (exception != null)
            //        {
            //            Console.WriteLine($"ERROR - {exception.Message}");
            //        }

            //        (state as PipeWriter).Complete(exception);
            //    }, state: this.WritePipe.Writer);
        }

        private Pipe Pipe { get; }
        private Pipe ReadPipe { get; }
        private Pipe WritePipe { get; }

        public PipeReader Input => this.ReadPipe.Reader;
        public PipeWriter Output => this.WritePipe.Writer;

        public async Task ReadFile(string filePath)
        {
            if (String.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(message: $"{nameof(filePath)} is not provided.", paramName: nameof(filePath));
            }

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                while (true)
                {
                    //var buffer = this.WritePipe.Writer.GetMemory(1);
                    Memory<byte> buffer = this.Pipe.Writer.GetMemory(1);
                    int bytes = await fileStream.ReadAsync(buffer);
                    //this.WritePipe.Writer.Advance(bytes);
                    this.Pipe.Writer.Advance(bytes);
                    if (bytes == 0)
                    {
                        // source EOF
                        break;
                    }
                    //var flush = await this.WritePipe.Writer.FlushAsync();
                    var flush = await this.Pipe.Writer.FlushAsync();
                    if (flush.IsCompleted || flush.IsCanceled)
                    {
                        break;
                    }
                }

                this.Pipe.Writer.Complete();
            }
        }

        public async Task CopyToS3(string s3Path)
        {
            if (String.IsNullOrWhiteSpace(s3Path))
            {
                throw new ArgumentException(message: $"{nameof(s3Path)} is not provided.", paramName: nameof(s3Path));
            }

            //this.WritePipe.Writer.OnReaderCompleted(
            //    callback: (exception, state) =>
            //    {
            //        if(exception != null)
            //        {
            //            Console.WriteLine($"ERROR - {exception.Message}");
            //        }

            //        (state as PipeReader).Complete(exception);
            //    }, state: this.ReadPipe.Reader);

            //this.ReadPipe.Reader.OnWriterCompleted(
            //    callback: (exception, state) =>
            //    {
            //        if(exception != null)
            //        {
            //            Console.WriteLine($"ERROR - {exception.Message}");
            //        }

            //        (state as PipeWriter).Complete(exception);
            //    }, state: this.WritePipe.Writer);

            while(true)
            {
                //ReadResult result = await this.ReadPipe.Reader.ReadAsync();
                ReadResult result = await this.Pipe.Reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;
                if(result.IsCompleted && buffer.IsEmpty)
                {
                    break;
                }

                using (var file = new FileStream(s3Path, FileMode.Append, FileAccess.Write))
                {
                    foreach (var segment in buffer)
                    {
                        // append it to the file
                        bool leased = false;
                        if (!MemoryMarshal.TryGetArray(segment, out var arr))
                        {
                            byte[] temporary = ArrayPool<byte>.Shared.Rent(segment.Length);
                            segment.CopyTo(temporary);
                            arr = new ArraySegment<byte>(temporary, 0, segment.Length);
                            leased = true;
                        }
                        await file.WriteAsync(arr.Array, arr.Offset, arr.Count);
                        await file.FlushAsync();
                        if (leased)
                        {
                            ArrayPool<byte>.Shared.Return(arr.Array);
                        }

                        // and flush it upstream
                        //await this.WritePipe.Writer.WriteAsync(segment);
                        //await this.Pipe.Writer.WriteAsync(segment);
                    }
                }
                //this.ReadPipe.Reader.AdvanceTo(buffer.End);
                this.Pipe.Reader.AdvanceTo(buffer.End);
            }
        }
    }
}
