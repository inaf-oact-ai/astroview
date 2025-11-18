using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace AstroView.WebApp.App.Utils;

public static class ExtensionMethods
{
    public static bool IsEmpty(this string me)
    {
        return string.IsNullOrWhiteSpace(me);
    }

    public static bool IsNotEmpty(this string me)
    {
        return !string.IsNullOrWhiteSpace(me);
    }

    public static string ReverseStr(this string me)
    {
        return new string(me.Reverse().ToArray());
    }

    public static IList<T> EmptyIfNull<T>(this IList<T> me)
    {
        return me ?? new List<T>();
    }

    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> me)
    {
        return me ?? new List<T>();
    }

    public static string ToListString<T>(this IList<T> me, char sep = ',')
    {
        return me.Select(r => r!.ToString()!).Aggregate((a, b) => a + sep + b);
    }

    public static string GetUserId(this ClaimsPrincipal principal)
    {
        if (principal == null)
        {
            throw new ArgumentNullException(nameof(principal));
        }
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier);
        return claim!.Value;
    }

    public static async Task<string> GetUserId(this AuthenticationStateProvider me)
    {
        var state = await me.GetAuthenticationStateAsync();
        return state.User.GetUserId();
    }

    private static string[] Sizes = { "Bytes", "Kb", "Mb", "Gb", "Tb" };
    public static string FileSizeEx(this FileInfo fi)
    {
        var bytes = fi.Length;
        if (bytes == 0)
            return "0 Bytes";
        var i = (int)Math.Floor(Math.Log(bytes) / Math.Log(1024));
        return Math.Round(bytes / Math.Pow(1024, i), 2) + " " + Sizes[i];
    }

    public static string UnixFormat(this string me)
    {
        return me.Replace('\\', '/');
    }

    public static string FormatJson(this string me)
    {
        var parsedJson = JsonSerializer.Deserialize<dynamic>(me);
        return JsonSerializer.Serialize(parsedJson, new JsonSerializerOptions { WriteIndented = true });
    }
}
