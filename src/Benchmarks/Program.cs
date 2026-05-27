using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

var config = ManualConfig.CreateMinimumViable()
    .AddExporter(MarkdownExporter.GitHub)
    .AddLogger(ConsoleLogger.Default)
    .WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(40));

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
