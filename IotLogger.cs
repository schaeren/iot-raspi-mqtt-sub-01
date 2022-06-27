using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Iot.Raspi.Logger
{
    // Used to initialize serilog.
    public class IotLogger
    {
        public static void Init(string configFilename)
        {
            IConfiguration configSerilog = new ConfigurationBuilder()
                .AddJsonFile(configFilename, false, true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configSerilog)
                .Enrich.WithThreadId()
                .Enrich.FromLogContext()
                .CreateLogger();
        }
    }

    // Idea from https://stackoverflow.com/questions/29470863/serilog-output-enrich-all-messages-with-methodname-from-which-log-entry-was-ca
    // Extension method for Serilog.ILogger, it automatically extends the logger 
    // context with information about class name, method name, source file path and 
    // line number. This information may be written to log outputs (see configuration).
    public static class LoggerExtensions
    {
        public static ILogger Here(
            this ILogger logger,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
            )
        {
            if (Path.DirectorySeparatorChar == '/')
            {
                // If we are using cross-development on Windows for Linux: File paths are 
                // taken from compiler/IDE environment on Windows ...
                // ... but the following GetFileNameWithoutExtension() is going to be executed 
                // on a Linux target system. So we have to fix the delimiters.
                sourceFilePath = sourceFilePath.Replace('\\', '/');
            }
            var className = Path.GetFileNameWithoutExtension(sourceFilePath);

            return logger
                .ForContext("ClassName", className)
                .ForContext("MemberName", memberName)
                .ForContext("FilePath", sourceFilePath)
                .ForContext("LineNumber", sourceLineNumber);
        }
    }
}