using CommandLine;
using Serilog.Events;

namespace Cosmic.Commands
{
    public abstract class OperationOptions
    {
        private LogEventLevel _logLevel = LogEventLevel.Information;

        [Option('v', "log-level", HelpText = "logging level")]
        public string LogLevelArg
        {
            set
            {
                LogEventLevel level;
                if (LogEventLevel.TryParse(value, true, out level))
                {
                    _logLevel = level;
                }
            }
        }

        public LogEventLevel LogLevel
        {
            get => _logLevel;
        }

        [Option('c', "container-path", HelpText = "Container path e.g. <connection>/<database>/<container>")]
        public string ContainerPath { get; set; }

        [Option('r', "output-request-charge", HelpText = "Output request charge (in RUs).")]
        public bool OutputRequestCharge { get; set; }

        [Option('l', "loop", Default = 1, HelpText = "Loops this operation multiple specified times with %I% as the iterator.")]
        public int Loop { get; set; }
    }
}