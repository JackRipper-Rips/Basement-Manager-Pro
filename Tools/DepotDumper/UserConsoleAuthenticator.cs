using System;
using System.Threading.Tasks;
using SteamKit2.Authentication;

namespace SolusManifestApp.Tools.DepotDumper
{
    class UserConsoleAuthenticator : IAuthenticator
    {
        public event Action<string>? OnPrompt;

        public async Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        {
            return await Task.Run(() =>
            {
                if (previousCodeWasIncorrect)
                {
                    OnPrompt?.Invoke("The previous code was incorrect. Please enter a new code:");
                }
                else
                {
                    OnPrompt?.Invoke("Please enter your 2FA code:");
                }

                return Console.ReadLine() ?? string.Empty;
            });
        }

        public async Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        {
            return await Task.Run(() =>
            {
                if (previousCodeWasIncorrect)
                {
                    OnPrompt?.Invoke($"The previous code sent to {email} was incorrect. Please enter the new code:");
                }
                else
                {
                    OnPrompt?.Invoke($"Please enter the code sent to {email}:");
                }

                return Console.ReadLine() ?? string.Empty;
            });
        }

        public async Task<bool> AcceptDeviceConfirmationAsync()
        {
            // For QR code authentication, this should just return true
            // The QR code flow doesn't require explicit confirmation
            return await Task.FromResult(true);
        }
    }
}
