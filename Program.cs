using System;
using System.Windows.Forms;

namespace Spotify_Audio_Controller
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Add crash logging
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => 
            {
                Form1.Log("Unhandled UI Exception: " + e.Exception.ToString());
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => 
            {
                Form1.Log("Unhandled Domain Exception: " + e.ExceptionObject.ToString());
            };

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}