using Cosmic.Aliases;
using Cosmic.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;

namespace Cosmic.Commands.Upsert
{
    public class UpsertCommand : OperationCommand<UpsertOptions>
    {
        private readonly LoggingLevelSwitch _levelSwitch;

        public UpsertCommand(LoggingLevelSwitch levelSwitch)
        {
            _levelSwitch = levelSwitch;
        }
        private int loaded = 0; 
        protected async override Task<int> ExecuteCommandAsync(UpsertOptions options)
        {
            _levelSwitch.MinimumLevel = options.LogLevel;
            await base.ExecuteCommandAsync(options);

            IEnumerable<object> docs = null;

            if (options.File == null)
            {
                var parameters = new string[]
                {
                    options.Value1, options.Value2, options.Value3,
                    options.Value4, options.Value5, options.Value6,
                    options.Value7, options.Value8, options.Value9
                };

                var paramId = 0;

                var documents = options.Documents;

                parameters
                    .TakeWhile(x => x != null)
                    .ToList()
                    .ForEach(x => {
                        paramId++;
                        documents = documents.ReplaceFirst("%%", x);
                    });

                documents = new AliasProcessor().Process(documents, DateTime.UtcNow, iterator);

                docs = new object[] { JsonConvert.DeserializeObject<object>(documents) };
            }
            else
            {
                docs =
                    (await File.ReadAllLinesAsync(options.File))
                    .Select(x => JsonConvert.DeserializeObject(x));
            }

            var docArray = docs.ToArray();
            var count = docArray.Count();

            Console.WriteLine($"Upserting {count} documents.");

            var jobs =
                docArray
                    .AsParallel()
                    .Select(async (doc, index) =>
                    {
                        if (options.OutputDocument)
                        {
                            Console.WriteLine(JsonConvert.SerializeObject(doc));
                        }

                        //Console.WriteLine($"Uploading doc {index}");
                        var result = await Container.UpsertItemAsync(doc);

                        if ((int) result.StatusCode >= 200 && (int) result.StatusCode <= 299)
                        {
                            //Console.WriteLine($"completed uploading doc {index}");
                            Interlocked.Increment(ref loaded);
                            var value = Volatile.Read(ref loaded);
                            if (value % 100 == 0)
                            {
                                Log.Debug($"Upserted {value}/{count} documents.");
                            }
                        }
                    })
                    .WithDegreeOfParallelism(options.Parallelism);
            await Task.WhenAll(jobs);
            Console.WriteLine($"Upserted {Volatile.Read(ref loaded)}/{count} documents.");
            // foreach (var doc in docs)
            // {
            //     //Console.WriteLine(JsonConvert.SerializeObject(doc));
            //     var result = await Container.UpsertItemAsync(doc);
            //     LogRequestCharge(result.RequestCharge);
            //     if ((int)result.StatusCode >= 200 && (int)result.StatusCode <= 299)
            //     {
            //         loaded++;
            //     }
            // }

            // Console.WriteLine($"Upserted {loaded}/{count} documents.");

            return 0;
        }
    }
}
