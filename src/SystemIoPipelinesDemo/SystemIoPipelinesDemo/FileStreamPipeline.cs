using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    public class FileStreamPipeline : IPipeline
    {
        public FileStreamPipeline()
        {
        }

        /// <summary>
        /// Read file from file system using <see cref="FileStream"/>
        /// </summary>
        /// <param name="pipe"></param>
        /// <param name="path">File Path to be read</param>
        /// <param name="cancellationTokenSource"></param>
        /// <returns></returns>
        public async Task Read(Pipe pipe, string path, CancellationTokenSource cancellationTokenSource)
        {
            if(pipe == null)
            {
                cancellationTokenSource.Cancel();
                throw new ArgumentException(
                    message: $"[{nameof(pipe)}] is not provided."
                    , paramName: nameof(pipe));
            }

            if (String.IsNullOrWhiteSpace(path))
            {
                cancellationTokenSource.Cancel();
                throw new ArgumentException(
                    message: $"[{nameof(path)}] is not provided."
                    , paramName: nameof(path));
            }

            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
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

        /// <summary>
        /// Write/Save file into file system using <see cref="FileStream"/>
        /// </summary>
        /// <param name="pipe"></param>
        /// <param name="path">File path to written into</param>
        /// <param name="cancellationTokenSource"></param>
        /// <returns></returns>
        public async Task Write(Pipe pipe, string path, CancellationTokenSource cancellationTokenSource)
        {
            if (pipe == null)
            {
                cancellationTokenSource.Cancel();
                throw new ArgumentException(
                    message: $"[{nameof(pipe)}] is not provided."
                    , paramName: nameof(pipe));
            }
            if (String.IsNullOrWhiteSpace(path))
            {
                cancellationTokenSource.Cancel();
                throw new ArgumentException(
                    message: $"[{nameof(path)}] is not provided."
                    , paramName: nameof(path));
            }
            if(File.Exists(path))
            {
                cancellationTokenSource.Cancel();
                throw new ArgumentException(
                    message: $"[{nameof(path)}: \"{path}\"] is already exists. Please provide different path."
                    , paramName: nameof(path));
            }
            using (var file = new FileStream(path, FileMode.Append, FileAccess.Write))
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
                            arraySegment = new ArraySegment<byte>(temporary, offset: 0, count: segment.Length);
                            leased = true;
                        }
                        await file.WriteAsync(arraySegment.Array, arraySegment.Offset, arraySegment.Count, cancellationTokenSource.Token);
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
