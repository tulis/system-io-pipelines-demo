using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SystemIoPipelinesDemo
{
    public class ReadS3StreamPipeline : IStreamPipeline
    {
        public ReadS3StreamPipeline(string bucket, string key)
        {
            if (String.IsNullOrWhiteSpace(bucket))
            {
                throw new ArgumentNullException(
                    message: $"[{nameof(bucket)}] is not provided."
                    , paramName: nameof(bucket));
            }

            if (String.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(
                    message: $"[{nameof(key)}] is not provided."
                    , paramName: nameof(key));
            }

            this.Bucket = bucket;
            this.Key = key;
            this.GetObjectRequest = new GetObjectRequest()
            {
                BucketName = bucket
                , Key = key
            };
        }

        public string Bucket { get; }
        public string Key { get; }
        public GetObjectRequest GetObjectRequest { get; }

        public async Task Stream(Pipe pipe, CancellationTokenSource cancellationTokenSource)
        {
            if (pipe == null)
            {
                cancellationTokenSource.Cancel();
                throw new ArgumentNullException(
                    message: $"[{nameof(pipe)}] is not provided."
                    , paramName: nameof(pipe));
            }


            using (var s3Client = Config.Get<IAmazonS3>())
            using (var getObjectResponse = await s3Client.GetObjectAsync(
                this.GetObjectRequest
                , cancellationTokenSource.Token))
            using (var stream = getObjectResponse.ResponseStream)
            {
                while (true)
                {
                    Memory<byte> buffer = pipe.Writer.GetMemory(sizeHint: 1);
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
