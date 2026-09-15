using HTTProof.Models;
using HTTProof.ViewModels;

namespace HTTProof.Services;

public static class CliBootstrap
{
    public static void ApplyTo(MainViewModel vm, CliOptions o)
    {
        if (o.Method is not null) vm.Method = o.Method;
        if (o.Url is not null) vm.Url = o.Url;

        foreach (var (k, v) in o.Params)
            vm.Params.Add(new KeyValueEntry { Key = k, Value = v });

        foreach (var (k, v) in o.Headers)
            vm.Headers.Add(new KeyValueEntry { Key = k, Value = v });

        if (o.Auth is { } auth)
        {
            vm.Auth = auth;
            if (o.AuthUser is not null) vm.AuthUser = o.AuthUser;
            if (o.AuthSecret is not null) vm.AuthSecret = o.AuthSecret;
        }

        if (o.Body is { } body)
        {
            vm.Body = body;
            if (o.BodyText is not null) vm.BodyText = o.BodyText;
        }
    }

}
