using System.Runtime.InteropServices;
using System.Windows;

namespace Community.PowerToys.Run.Plugin.CommandMan;

internal static class ClipboardService
{
    public static bool TryCopy(string text)
    {
        var copied = false;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    copied = true;
                    return;
                }
                catch (ExternalException ex)
                {
                    failure = ex;
                    Thread.Sleep(30 * (attempt + 1));
                }
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        _ = failure;
        return copied;
    }
}
