using System;
using System.Linq;
using System.Windows.Forms;

namespace DNDStrongholdApp;

static class Program
{
    public static bool DebugMode { get; private set; } = false;
    public static bool TestMode { get; private set; } = false;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Check for debug flag
        DebugMode = args.Contains("d");
        TestMode = args.Contains("p");

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