namespace Glaze;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Resources.Extract();
        Application.Run(new MainForm());
    }
}
