using System.Collections.Generic;
using System.Collections.ObjectModel;
using HTTProof.Models;
using HTTProof.ViewModels;

namespace HTTProof.Services;

public static class StateMapper
{
    public static StateDto ToDto(MainViewModel vm)
    {
        var dto = new StateDto
        {
            IsBusy = vm.IsBusy,
            HasResponse = vm.HasResponse,
            Error = vm.ErrorMessage,
            Request = new RequestDto
            {
                Method = vm.Method,
                Url = vm.Url,
                Params = KvList(vm.Params),
                Headers = KvList(vm.Headers),
                Auth = new AuthDto { Kind = vm.Auth, User = vm.AuthUser, Secret = vm.AuthSecret },
                Body = new BodyDto { Kind = vm.Body, Text = vm.BodyText },
            },
        };

        if (vm.HasResponse && vm.StatusCode is { } status)
        {
            dto.Response = new ResponseDto
            {
                Status = status,
                StatusText = vm.StatusText,
                Headers = new Dictionary<string, string>(vm.ResponseHeaders),
                Body = vm.ResponseBody,
                ContentType = vm.ResponseContentType,
                DurationMs = vm.DurationMs ?? 0,
                ByteLength = vm.ByteLength ?? 0,
            };
        }

        return dto;
    }

    public static void ApplyPatch(MainViewModel vm, RequestDto patch)
    {
        if (patch.Method is not null) vm.Method = patch.Method;
        if (patch.Url is not null) vm.Url = patch.Url;
        if (patch.Params is not null) Replace(vm.Params, patch.Params);
        if (patch.Headers is not null) Replace(vm.Headers, patch.Headers);
        if (patch.Auth is not null)
        {
            vm.Auth = patch.Auth.Kind;
            if (patch.Auth.User is not null) vm.AuthUser = patch.Auth.User;
            if (patch.Auth.Secret is not null) vm.AuthSecret = patch.Auth.Secret;
        }
        if (patch.Body is not null)
        {
            vm.Body = patch.Body.Kind;
            if (patch.Body.Text is not null) vm.BodyText = patch.Body.Text;
        }
    }

    private static List<KvDto> KvList(ObservableCollection<KeyValueEntry> src)
    {
        var list = new List<KvDto>(src.Count);
        foreach (var e in src) list.Add(new KvDto { Enabled = e.Enabled, Key = e.Key, Value = e.Value });
        return list;
    }

    private static void Replace(ObservableCollection<KeyValueEntry> dst, List<KvDto> src)
    {
        dst.Clear();
        foreach (var e in src) dst.Add(new KeyValueEntry { Enabled = e.Enabled, Key = e.Key, Value = e.Value });
    }
}
