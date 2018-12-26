using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using System;

namespace SystemIoPipelinesDemo
{
    // https://github.com/Microsoft/ApplicationInsights-aspnetcore/wiki/Getting-Started-for-a-.NET-Core-console-application
    public static class Config
    {
        private static Lazy<IConfiguration> LazyConfiguration
            = new Lazy<IConfiguration>(()
            =>
            {
                return new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();
            });

        public static string Get(string key)
        {
            return LazyConfiguration.Value[key];
        }

        // https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-netcore.html
        public static T Get<T>() where T: IAmazonService
        {
            var awsOptions = LazyConfiguration.Value.GetAWSOptions();
            var client = awsOptions.CreateServiceClient<T>();
            return client;
        }
    }
}
