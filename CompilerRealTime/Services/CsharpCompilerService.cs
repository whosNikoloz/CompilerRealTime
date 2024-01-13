using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.AspNetCore.SignalR;
using CompilerRealTime.Models;
using CompilerRealTime.Hubs;

namespace CompilerRealTime.Services
{
    public class CsharpCompilerService
    {
        private readonly IHubContext<CompilerHub> _hubContext;

        public CsharpCompilerService(IHubContext<CompilerHub> hubContext)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task CompileAndExecute(string code, string input)
        {
            try
            {
                // Create a temporary directory
                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                // Create a C# source code file
                string sourceFilePath = Path.Combine(tempDir, "Program.cs");
                File.WriteAllText(sourceFilePath, code);

                // Define compilation options
                var compilationOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication);

                // Add references to required assemblies
                var references = new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime.Extensions").Location)
                };

                // Create a compilation context with syntax trees and references
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                var compilation = CSharpCompilation.Create("OnlineCompilerAssembly")
                    .WithOptions(compilationOptions)
                    .AddReferences(references)
                    .AddSyntaxTrees(syntaxTree);

                using (var stream = new MemoryStream())
                {
                    var emitResult = compilation.Emit(stream);

                    await SendCompilationUpdate(emitResult, stream);
                }
            }
            catch (Exception ex)
            {
                await SendCompilationError(ex.Message);
            }
        }

        private async Task SendCompilationUpdate(EmitResult emitResult, MemoryStream stream)
        {
            if (emitResult.Success)
            {
                stream.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(stream.ToArray());

                var entryPointMethod = assembly.EntryPoint;
                if (entryPointMethod != null)
                {
                    using (var outputWriter = new StringWriter())
                    using (var errorWriter = new StringWriter())
                    {
                        var originalOut = Console.Out;
                        var originalError = Console.Error;

                        try
                        {
                            Console.SetOut(outputWriter);
                            Console.SetError(errorWriter);

                            Console.Clear(); // Clear existing console output

                            entryPointMethod.Invoke(null, new object[] { new string[] { } });
                        }
                        finally
                        {
                            Console.SetOut(originalOut);
                            Console.SetError(originalError);
                        }

                        await _hubContext.Clients.All.SendAsync("ReceiveCompilationUpdate", new CompilationUpdate
                        {
                            Success = true,
                            Output = outputWriter.ToString(),
                            Error = errorWriter.ToString()
                        });
                    }
                }
                else
                {
                    await SendCompilationError("No suitable entry point found in the code.");
                }
            }
            else
            {
                await SendCompilationError(string.Join(Environment.NewLine, emitResult.Diagnostics));
            }
        }

        private async Task SendCompilationError(string errorMessage)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveCompilationUpdate", new CompilationUpdate
            {
                Success = false,
                Output = errorMessage,
                Error = errorMessage
            });
        }
    }
}