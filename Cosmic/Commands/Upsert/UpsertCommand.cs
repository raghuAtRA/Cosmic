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
using System.Threading.Channels;

namespace Cosmic.Commands.Upsert
{
    public class UpsertCommand : OperationCommand<UpsertOptions>
    {
        private readonly LoggingLevelSwitch _levelSwitch;
        private readonly Channel<object> dataChannel;
        private ChannelWriter<object> writer;
        private ChannelReader<object> _reader;
        public UpsertCommand(LoggingLevelSwitch levelSwitch)
        {
            _levelSwitch = levelSwitch;
            var channelOptioons = new BoundedChannelOptions(1000)
            {
                SingleReader = false,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            };
            dataChannel = Channel.CreateBounded<object>(channelOptioons);
            _reader = dataChannel.Reader;
        }

        private async Task WriterTask(string file)
        {
            Log.Debug("Starting publisher with file {file}", file);

            var lineNo = 1;
            using (var sr = new StreamReader(file))
            {
                while (!sr.EndOfStream)
                {
                    var line = await sr.ReadLineAsync();
                    Log.Debug("Wrote line {line}", lineNo++);
                    var json = JsonConvert.DeserializeObject(line);
                    await writer.WriteAsync(json);
                }
                writer.Complete();
            }
        }
        private async ValueTask Process(UpsertOptions options)
        {
            while (await _reader.WaitToReadAsync())
            {
                await foreach(object json in _reader.ReadAllAsync())
                {
                    if (options.OutputDocument)
                    {
                        Console.WriteLine(JsonConvert.SerializeObject(json));
                    }

                    //Console.WriteLine($"Uploading doc {index}");
                    var result = await Container.UpsertItemAsync(json);

                    if ((int) result.StatusCode >= 200 && (int) result.StatusCode <= 299)
                    {
                        //Console.WriteLine($"completed uploading doc {index}");
                        Interlocked.Increment(ref loaded);
                        var value = Volatile.Read(ref loaded);
                        Log.Debug("Upserted {count} document ", value);
                        if (value % 100 == 0)
                        {
                            Log.Debug($"Upserted {value} documents.");
                        }
                    }
                    
                }
            }
        }
        private int loaded = 0;

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

            var writerTask = WriterTask(options.File);
            var readerTask = Process(options).AsTask();
            //var docArray = docs
            await Task.WhenAll(writerTask, readerTask);
            Console.WriteLine($"Upserted {Volatile.Read(ref loaded)} documents.");

            return 0;
        }
    }
}
