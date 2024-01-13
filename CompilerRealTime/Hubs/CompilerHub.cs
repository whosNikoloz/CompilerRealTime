using Microsoft.AspNetCore.SignalR;
using CompilerRealTime.Services;

using CompilerRealTime.Models;

namespace CompilerRealTime.Hubs
{
    public class CompilerHub : Hub
    {
        private readonly CsharpCompilerService _compilerService;
        private readonly ILogger<CompilerHub> _logger; // Add a logger field

        public CompilerHub(CsharpCompilerService compilerService, ILogger<CompilerHub> logger)
        {
            _compilerService = compilerService ?? throw new ArgumentNullException(nameof(compilerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CompileAndExecuteCode(string code, string input)
        {
            try
            {
                await _compilerService.CompileAndExecute(code, input);
            }
            catch (Exception ex)
            {
                // Log the exception instead of writing to the console
                _logger.LogError(ex, "Error during code compilation and execution");
                // You can also send an error message to the client if needed
                await Clients.Caller.SendAsync("CompilationError", ex.Message);
            }
        }

        public override Task OnConnectedAsync()
        {
            // Perform any connection-specific logic
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            // Perform any disconnection-specific logic
            return base.OnDisconnectedAsync(exception);
        }
    }
}
