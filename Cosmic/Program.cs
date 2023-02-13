using System.Threading.Tasks;
using Serilog;
using Serilog.Core;

namespace Cosmic
{
    class Program
    {
        static Task<int> Main(string[] args)
        {
            var logSwitch = new LoggingLevelSwitch();
            Log.Logger =
                new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(logSwitch)
                    .WriteTo.Console()
                    .CreateLogger();
            return new Runtime().ExecuteAsync(args, logSwitch);
        }
    }
}
