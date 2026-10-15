using System.Globalization;
using System.Text;
namespace DivineLabs.IdleSlayer.Maintenance;

// Progress is advisory. A busy reader must never roll back an otherwise valid installation.
public static class OperationReport
{
    public static bool TryWrite(string path, string message, int percent)
    {
        if (string.IsNullOrEmpty(path)) return true;
        string temporary = path + ".next";
        try
        {
            File.WriteAllText(temporary, percent.ToString(CultureInfo.InvariantCulture) + "\n" +
                message.Replace('\r', ' ').Replace('\n', ' '), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static bool TryRead(string path, out int percent, out string message)
    {
        percent = -1;
        message = "";
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(input, Encoding.UTF8);
            string number = reader.ReadLine();
            string text = reader.ReadLine();
            if (text == null || !int.TryParse(number, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int parsed) || parsed < -1 || parsed > 100) return false;
            percent = parsed;
            message = text;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
