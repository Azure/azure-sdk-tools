using System;
using System.Diagnostics;
using System.Text;
using Azure.Sdk.Tools.SecretRotation.Stores.Generic;

namespace Azure.Sdk.Tools.SecretManagement.Cli;

internal class ConsoleValueProvider : IUserValueProvider
{
    public string GetValue(string prompt, bool secret)
    {
        Console.Write($"{prompt}: ");
        StringBuilder keysPressed = new();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(secret);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return keysPressed.ToString();
                case ConsoleKey.Backspace:
                    {
                        if (keysPressed.Length > 0)
                        {
                            Console.Write(' ');
                            Console.Write(key.KeyChar);
                            keysPressed.Length -= 1;
                        }

                        break;
                    }
                default:
                    keysPressed.Append(key.KeyChar);
                    break;
            }
        }
    }

    public void PromptUser(string prompt, string? oldValue, string? newValue)
    {
        Console.WriteLine();
        Console.WriteLine(prompt);

        bool promptNew = !string.IsNullOrEmpty(newValue);
        bool promptOld = !string.IsNullOrEmpty(oldValue);

        if (promptOld)
        {
            Console.WriteLine("Press Ctrl-O to copy the old value to the clipboard.");
        }

        if (promptNew)
        {
            Console.WriteLine("Press Ctrl-N to copy the new value to the clipboard.");
        }

        Console.WriteLine("Press Enter to continue.");

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(true);

            if (key is { Key: ConsoleKey.Enter })
            {
                break;
            }

            if (promptOld && key is { Modifiers: ConsoleModifiers.Control, Key: ConsoleKey.O })
            {
                SetClipboard(oldValue!);
            }

            if (promptNew && key is { Modifiers: ConsoleModifiers.Control, Key: ConsoleKey.N })
            {
                SetClipboard(newValue!);
            }
        }
    }

    private static void SetClipboard(string value)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true
            }
        };

        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") != null)
        {
            process.StartInfo.FileName = "bash";
            process.StartInfo.Arguments = $"-c \"echo '{value}' | clip.exe\"";
        }
        else if (OperatingSystem.IsLinux())
        {
            process.StartInfo.FileName = "bash";
            process.StartInfo.Arguments = $"-c \"echo '{value}' | xsel -i --clipboard\"";
        }
        else if (OperatingSystem.IsMacOS())
        {
            process.StartInfo.FileName = "bash";
            process.StartInfo.Arguments = $"-c \"echo '{value}' | pbcopy\"";
        }
        else if (OperatingSystem.IsWindows())
        {
            process.StartInfo.FileName = "cmd";
            process.StartInfo.Arguments = $"/c \"echo {value} | clip.exe\"";
        }
        else
        {
            Console.WriteLine($"Failed to copy to clipboard. Unsupported OS {Environment.OSVersion.Platform}");
        }

        process.Start();
    }
}