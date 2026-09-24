using System;
using System.Linq;
using System.Windows.Forms;

namespace DNDStrongholdApp;

static class Program
{
    public static bool DebugMode { get; private set; } = false;
    public static bool TestMode { get; private set; } = false;
    /// <summary>Same as TestMode (p), then simulate four turns into a mid-game playthrough state.</summary>
    public static bool PlaythroughMode { get; private set; } = false;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Check for debug / playtest flags
        //   d  — debug message boxes during startup
        //   p  — load TestStrongholdData.json
        //   p1 — same as p, then advance to week 4 with history and ongoing work
        DebugMode = args.Contains("d");
        PlaythroughMode = args.Contains("p1");
        TestMode = args.Contains("p") || PlaythroughMode;

        try
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new MainDashboard());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw; // Re-throw to see the error in the console
        }
    }    
}