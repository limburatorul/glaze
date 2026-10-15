using System.Diagnostics;
using System.IO.Pipes;

namespace Glaze;

internal static class Program
{
    // One Glaze per Windows session: a second start (Reel's "Trailers on YouTube", a shortcut) hands its
    // address to the open window over this pipe and quits.
    private static readonly string Name = $"ProtagonistLabs.Glaze.{Process.GetCurrentProcess().SessionId}";

    [STAThread]
    private static void Main(string[] args)
    {
        // Only a youtube.com page; anything else on the command line is ignored.
        var url = args.FirstOrDefault(MainForm.OnYouTube);
        using var mutex = new Mutex(true, $@"Local\{Name}", out var first);
        if (!first)
        {
            HandOff(url);
            return;
        }
        ApplicationConfiguration.Initialize();
        Resources.Extract();
        Ecosystem.WriteCard();
        var form = new MainForm(url);
        form.Load += (_, _) => _ = Listen(form);
        Application.Run(form);
    }

    /// <summary>Tells the open Glaze to come forward and open the address (an empty line: just come
    /// forward). If it doesn't answer, this start simply ends.</summary>
    private static void HandOff(string? url)
    {
        // Started by the user, this process may give the foreground to the open window.
        Native.AllowSetForegroundWindow(Native.ASFW_ANY);
        try
        {
            using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(5000);
            using var writer = new StreamWriter(pipe);
            writer.WriteLine(url ?? "");
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Hand-off failed: {ex.Message}");
        }
    }

    /// <summary>Waits for later starts, one at a time, for as long as Glaze runs.</summary>
    private static async Task Listen(MainForm form)
    {
        while (!form.IsDisposed)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(Name, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync();
                using var reader = new StreamReader(pipe);
                var line = await reader.ReadLineAsync() ?? "";
                // Checked again here: the pipe is this user's alone, but what comes through it is still input.
                form.Open(MainForm.OnYouTube(line) ? line : null);
            }
            catch (IOException ex)
            {
                // A start that went away mid-message; wait for the next one.
                Debug.WriteLine($"Hand-off dropped: {ex.Message}");
            }
        }
    }
}
