using System.Text;
using System.Windows.Forms;
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
        Thread thread = new(() => Clipboard.SetText(value));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
