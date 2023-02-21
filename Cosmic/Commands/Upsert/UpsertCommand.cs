using Cosmic.Aliases;
using Cosmic.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using System.Threading.Channels;

namespace Cosmic.Commands.Upsert
{
    public class UpsertCommand : OperationCommand<UpsertOptions>
    {
        private readonly LoggingLevelSwitch _levelSwitch;
        private readonly Channel<object> dataChannel;
        private ChannelWriter<object> writer;
        private ChannelReader<object> _reader;
        private int count = 0;
        public UpsertCommand(LoggingLevelSwitch levelSwitch)
        {
            _levelSwitch = levelSwitch;
            dataChannel = Channel.CreateBounded<object>(100);
            _reader = dataChannel.Reader;
        }

        private async Task PublisherTask(string file)
        {

            var sw = new Stopwatch();
            sw.Start();
            var tasks = new List<Task>();
            using (var sr = new StreamReader(file))
            {
                while (!sr.EndOfStream)
                {
                    var line = await sr.ReadLineAsync();
                    Log.Verbose("Wrote line {line}", count++);
                    var json = JsonConvert.DeserializeObject(line);
                    tasks.Add(writer.WriteAsync(json).AsTask());
                }
            }

            await Task.WhenAll(tasks)
            .ContinueWith(t =>
                {
                    sw.Stop();
                    Log.Information("Producer: {file}; Count: {count}: Elapsed: {elapsed}ms", file, count,
                        sw.ElapsedMilliseconds);
                    writer.Complete();
                });
        }

        private int loaded = 0;
        private Task ProcessorTask(UpsertOptions options)
        {
            var sw = new Stopwatch(); sw.Start();
            var enu = _reader.ReadAllAsync();
            var tasks = 
                enu.Select(async json =>
                {
                    if (options.OutputDocument)
                    {
                        Console.WriteLine(JsonConvert.SerializeObject(json));
                    }

                    var result = await Container.UpsertItemAsync(json);

                    if ((int) result.StatusCode >= 200 && (int) result.StatusCode <= 299)
                    {
                        Interlocked.Increment(ref this.loaded);
                        var value = Volatile.Read(ref loaded);
                        if (options.Progress > 0 && value % options.Progress == 0)
                        {
                            if ((value / options.Progress) * options.Progress >= count)
                            {
                                Console.WriteLine(".");
                            }
                            else
                            {
                                Console.Write(".");
                            }
                            Log.Debug($"Upserted {value}/{count} documents.");
                        }
                    }
                    else
                    {
                        Log.Error("Error uploading document {status}", result.StatusCode);
                    }

                    return result;
                }).ToEnumerable();
            return Task.WhenAll(tasks)
                .ContinueWith(t =>
                {
                    var totalRus = t.Result.Sum(ir => ir.RequestCharge);
                    sw.Stop();
                    Log.Information("{count} documents upserted in {elapsed}s. Rate: {rate} docs/s, Total Rus {totalRus} ", count,
                        sw.Elapsed.TotalSeconds, count / sw.Elapsed.TotalSeconds, totalRus);

                });
        }

        protected async override Task<int> ExecuteCommandAsync(UpsertOptions options)
        {
            _levelSwitch.MinimumLevel = options.LogLevel;
            await base.ExecuteCommandAsync(options);

            writer = dataChannel.Writer;
            Task<IEnumerable<object>> docs = null;

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

                docs = Task.FromResult(new object[] {JsonConvert.DeserializeObject(documents)}.AsEnumerable());
            }
            else
            {
                // docs = await File.ReadAllLinesAsync(options.File);
            }

            var publisherTask = PublisherTask(options.File);
            var processorTask = ProcessorTask(options);
            //var docArray = docs
            await Task.WhenAll(publisherTask, processorTask);
            Console.WriteLine($"Upserted {Volatile.Read(ref loaded)} documents.");

            return 0;
        }
    }

}