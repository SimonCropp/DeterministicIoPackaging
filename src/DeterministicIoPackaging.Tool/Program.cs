return await new CommandLineApplicationBuilder()
    .AddCommandsFromThisAssembly()
    .SetExecutableName("detpackage")
    .SetTitle("DeterministicIoPackaging CLI")
    .SetDescription("Rewrites a System.IO.Packaging file (nupkg, xlsx, docx, pptx) so the same source package always produces byte-identical output.")
    .Build()
    .RunAsync();
