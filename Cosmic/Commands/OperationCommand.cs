﻿using Cosmic.Data;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Cosmic.Commands.Upsert;

namespace Cosmic.Commands
{
    public abstract class OperationCommand<TOptions> : Command<TOptions>
        where TOptions : OperationOptions
    {
        protected Container Container { get; private set; }

        protected int iterator;
        protected double requestCharge;

        public async override Task<int> ExecuteAsync(TOptions options)
        {
            await ExecuteBeforeAsync(options);
            for (int i = 0; i < options.Loop; i++)
            {
                iterator = i;
                await ExecuteCommandAsync(options);
                if (options.Loop > 1)
                {
                    Console.WriteLine($"Completed loop {i + 1}/{options.Loop}");
                }
            }
            await ExecuteAfterAsync(options);
            return 0;
        }

        protected async override Task<int> ExecuteCommandAsync(TOptions options)
        {
            var appDir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            var cosmicDir = appDir.CreateSubdirectory("cosmic");

            ContainerData containerData = null; 

            if (options.ContainerPath is null)
            {
                var activeContainerFile = await File.ReadAllTextAsync($"{cosmicDir}/activeContainer.json");
                containerData = JsonConvert.DeserializeObject<ContainerData>(activeContainerFile);
            }
            else
            {
                var path = options.ContainerPath.Split('/');
                containerData = new ContainerData
                {
                    ConnectionId = path[0].ToLowerInvariant(),
                    DatabaseId = path[1],
                    ContainerId = path[2]
                };
            }

            var connectionsDir = cosmicDir.CreateSubdirectory("connections");
            var connectionFile = await File.ReadAllTextAsync($"{connectionsDir}/{containerData.ConnectionId}.json");
            var connection = JsonConvert.DeserializeObject<ConnectionData>(connectionFile);

            var clientOptions = new CosmosClientOptions();
            if (options is UpsertOptions)
            {
                clientOptions.AllowBulkExecution = true;
                clientOptions.MaxRetryAttemptsOnRateLimitedRequests = 10;
                clientOptions.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromMinutes(5);
                
            }
            Container = new CosmosClient(connection.ConnectionString, clientOptions)
                .GetDatabase(containerData.DatabaseId)
                .GetContainer(containerData.ContainerId);
                

            return 0;
        }

        protected async override Task ExecuteAfterAsync(TOptions options)
        {
            if (options.OutputRequestCharge)
            {
                Console.WriteLine($"Request charge total was {requestCharge} RUs.");
            }

            await base.ExecuteAfterAsync(options);
        }

        protected void LogRequestCharge(double charge)
        {
            requestCharge += charge;
        }
    }
}
