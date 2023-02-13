﻿using System.Threading.Tasks;
using CommandLine;
using Cosmic.Commands.Accounts;
using Cosmic.Commands.Active;
using Cosmic.Commands.Aliases;
using Cosmic.Commands.Connect;
using Cosmic.Commands.Delete;
using Cosmic.Commands.Query;
using Cosmic.Commands.Store;
using Cosmic.Commands.Switch;
using Cosmic.Commands.Upsert;
using Serilog.Core;

namespace Cosmic
{
    public class Runtime
    {
        public async Task<int> ExecuteAsync(string[] args, LoggingLevelSwitch logswitch)
        {
            return await Parser.Default.ParseArguments<AccountsOptions, ActiveOptions, AliasesOptions, ConnectOptions, DeleteOptions, QueryOptions, StoreOptions, SwitchOptions, UpsertOptions>(args)
                .MapResult(
                  (AccountsOptions o) => new AccountsCommand().ExecuteAsync(o),
                  (ActiveOptions o) => new ActiveCommand().ExecuteAsync(o),
                  (AliasesOptions o) => new AliasesCommand().ExecuteAsync(o),
                  (ConnectOptions o) => new ConnectCommand().ExecuteAsync(o),
                  (DeleteOptions o) => new DeleteCommand().ExecuteAsync(o),
                  (QueryOptions o) => new QueryCommand().ExecuteAsync(o),
                  (StoreOptions o) => new StoreCommand().ExecuteAsync(o),
                  (SwitchOptions o) => new SwitchCommand().ExecuteAsync(o),
                  (UpsertOptions o) => new UpsertCommand(logswitch).ExecuteAsync(o),
                  errs => Task.FromResult(1));
        }
    }
}
