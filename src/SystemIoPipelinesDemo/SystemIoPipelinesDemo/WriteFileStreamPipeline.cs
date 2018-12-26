using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    public class WriteFileStreamPipeline : IStreamPipeline
    {
        public WriteFileStreamPipeline(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    message: $"[{nameof(path)}] is not provided."
                    , paramName: nameof(path));
            }

            if (File.Exists(path))
            {
                throw new ArgumentException(
                    message: $"[{nameof(path)}: \"{path}\"] is already exists. Please provide different path."
                    , paramName: nameof(path));
            }

            this.Path = path;
        }

        public string Path { get; }

        /// <summary>
        /// Write/Save file into file system using <see cref="FileStream"/>
        /// </summary>
        /// <param name="pipe"></param>
        /// <param name="cancellationTokenSource"></param>
        /// <returns></returns>
        public async Task Stream(Pipe pipe, CancellationTokenSource cancellationTokenSource)
        {
            if (pipe == null)
            {
                cancellationTokenSource.Cancel();
                throw new ArgumentException(
                    message: $"[{nameof(pipe)}] is not provided."
                    , paramName: nameof(pipe));
            }

            using (var file = new FileStream(this.Path, FileMode.Append, FileAccess.Write))
            {
                while (true)
                {
                    ReadResult result = await pipe.Reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    if (result.IsCompleted && buffer.IsEmpty)
                    {
                        break;
                    }

                    foreach (var segment in buffer)
                    {
                        // append it to the file
                        bool leased = false;
                        if (!MemoryMarshal.TryGetArray(segment, out var arraySegment))
                        {
                            byte[] temporary = ArrayPool<byte>.Shared.Rent(segment.Length);
                            segment.CopyTo(temporary);
                            arraySegment = new ArraySegment<byte>(
                                temporary
                                , offset: 0
                                , count: segment.Length);
                            leased = true;
                        }
                        await file.WriteAsync(arraySegment.Array
                            , arraySegment.Offset
                            , arraySegment.Count
                            , cancellationTokenSource.Token);

                        await file.FlushAsync(cancellationTokenSource.Token);
                        if (leased)
                        {
                            ArrayPool<byte>.Shared.Return(arraySegment.Array);
                        }
                    }

                    pipe.Reader.AdvanceTo(consumed: buffer.End);
                }

                pipe.Reader.Complete();
            }
        }
    }
}
